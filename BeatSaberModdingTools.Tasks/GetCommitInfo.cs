﻿using BeatSaberModdingTools.Tasks.Utilities;
using BeatSaberModdingTools.Tasks.Utilities.Mock;
using Microsoft.Build.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace BeatSaberModdingTools.Tasks
{
    /// <summary>
    /// Gets the git commit short hash of the project.
    /// </summary>
    public class GetCommitInfo : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// Captures the URL for the remote origin from a git config file.
        /// </summary>
        public static readonly Regex OriginSearch =
            new Regex(@"s*\[\s*remote\s*""origin""\s*\]\s*url\s*=\s*(.*)\s*", RegexOptions.IgnoreCase);
        /// <summary>
        /// Retrieves the branch name from the text returned by 'git status'.
        /// </summary>
        public static readonly Regex StatusBranchSearch = new Regex(@"^On branch (.*)$", RegexOptions.Multiline);
        /// <summary>
        /// Retrieves the detatched branch name from the text returned by 'git status'.
        /// </summary>
        public static readonly Regex DetatchedBranchSearch = new Regex(@"^HEAD detached at (.*)$", RegexOptions.Multiline);
        /// <summary>
        /// Text in 'git status' that indicates there are no changes in the repository to commit.
        /// </summary>
        private const string UnmodifiedText = "NOTHING TO COMMIT";
        /// <summary>
        /// Text in 'git status' that indicates there are no changes in the repository to commit, but there are untracked files.
        /// </summary>
        private const string UntrackedOnlyText = "NOTHING ADDED TO COMMIT";

        /// <summary>
        /// An object that retrieves text from the 'git' command.
        /// </summary>
        public IGitRunner GitRunner;

        /// <summary>
        /// The directory of the project.
        /// </summary>
        [Required]
        public virtual string ProjectDir { get; set; }
        /// <summary>
        /// Optional: Number of characters to retrieve from the hash.
        /// Default is 7.
        /// </summary>
        public virtual int HashLength { get; set; } = 7;
        /// <summary>
        /// Optional: If true, do not attempt to use 'git' executable.
        /// </summary>
        public virtual bool NoGit { get; set; }
        /// <summary>
        /// Optional: If true, do not attempt to check if files have been changed.
        /// </summary>
        public virtual bool SkipStatus { get; set; }
        /// <summary>
        /// Commit hash up to the number of characters set by <see cref="HashLength"/>.
        /// </summary>
        [Output]
        public virtual string CommitHash { get; protected set; }
        /// <summary>
        /// 'Modified' if the repository has uncommitted changes, 'Unmodified' if it doesn't. Will be left blank if unsupported (Only works if git bash is installed).
        /// </summary>
        [Output]
        public virtual string Modified { get; protected set; }
        /// <summary>
        /// Name of the current repository branch, if available.
        /// </summary>
        [Output]
        public virtual string Branch { get; protected set; }
        /// <summary>
        /// True if the branch appears to be a pull request.
        /// </summary>
        [Output]
        public virtual bool IsPullRequest { get; protected set; }
        /// <summary>
        /// URL for the repository's origin.
        /// Null/Empty if unavailable.
        /// </summary>
        [Output]
        public virtual string OriginUrl { get; protected set; }
        /// <summary>
        /// Username the repository belongs to.
        /// Null/Empty if unavailable.
        /// </summary>
        [Output]
        public virtual string GitUser { get; protected set; }

        /// <summary>
        /// <see cref="ITaskLogger"/> instance used.
        /// </summary>
        public ITaskLogger Logger;

        /// <summary>
        /// Attempts to retrieve the git commit hash using the 'git' program.
        /// </summary>
        /// <param name="gitRunner"></param>
        /// <param name="logger"></param>
        /// <param name="commitHash"></param>
        /// <returns></returns>
        public static bool TryGetGitCommit(IGitRunner gitRunner, ITaskLogger logger, out string commitHash)
        {
            commitHash = null;
            try
            {
                string outText = gitRunner.GetTextFromProcess(GitArgument.CommitHash);
                if (outText.Length > 0)
                {
                    commitHash = outText;
                    return true;
                }
            }
            catch (GitRunnerException ex)
            {
                logger?.LogWarning($"Error getting commit hash from 'git' command: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Attempts to check if the repository has uncommitted changes.
        /// </summary>
        /// <param name="gitRunner"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static GitInfo GetGitStatus(IGitRunner gitRunner, ITaskLogger logger)
        {
            GitInfo status = new GitInfo();
            try
            {
                string statusText = gitRunner.GetTextFromProcess(GitArgument.Status);
                Match match = StatusBranchSearch.Match(statusText);
                if (match.Success && match.Groups.Count > 1)
                {
                    string branch = match.Groups[1].Value;
                    status.Branch = branch;
                }
                else
                {
                    Match detatchedMatch = DetatchedBranchSearch.Match(statusText);
                    if(detatchedMatch.Success && detatchedMatch.Groups.Count > 1)
                    {
                        string branch = detatchedMatch.Groups[1].Value;
                        status.Branch = branch;
                        if (branch.StartsWith("pull/"))
                            status.IsPullRequest = true;
                    }
                }
                if (string.IsNullOrWhiteSpace(status.Branch))
                    logger.LogMessage(MessageImportance.High, $"Unable to retrieve branch name from status text: \n{statusText}");
                statusText = statusText.ToUpper();
                if (statusText.Contains(UnmodifiedText) || statusText.Contains(UntrackedOnlyText))
                    status.Modified = "Unmodified";
                else
                    status.Modified = "Modified";
            }
            catch (GitRunnerException ex)
            {
                logger?.LogWarning($"Error getting 'git status': {ex.Message}");
            }
            try
            {
                status.OriginUrl = gitRunner.GetTextFromProcess(GitArgument.OriginUrl);
                if (status.OriginUrl != null && status.OriginUrl.Length > 0)
                    status.GitUser = GetGitHubUser(status.OriginUrl);
            }
            catch (GitRunnerException ex)
            {
                logger?.LogWarning($"Error getting git origin URL: {ex.Message}");
            }
            return status;
        }

        /// <summary>
        /// Attempts to retrieve the git commit hash by reading git files.
        /// </summary>
        /// <param name="gitPath"></param>
        /// <param name="gitInfo"></param>
        /// <returns></returns>
        public static bool TryGetCommitManual(string gitPath, out GitInfo gitInfo)
        {
            gitInfo = new GitInfo();
            bool success = false;
            string headPath = Path.Combine(gitPath, "HEAD");
            string configPath = Path.Combine(gitPath, "config");
            if (File.Exists(headPath))
            {
                string headContents = File.ReadAllText(headPath);
                if (!string.IsNullOrEmpty(headContents) && headContents.StartsWith("ref:"))
                    headPath = Path.Combine(gitPath, headContents.Replace("ref:", "").Trim());
                gitInfo.Branch = headPath.Substring(headPath.LastIndexOf('/') + 1);
                if (File.Exists(headPath))
                {
                    headContents = File.ReadAllText(headPath);
                    if (headContents.Length >= 0)
                    {
                        gitInfo.CommitHash = headContents.Trim();
                        success = true;
                    }
                }
            }
            if (File.Exists(configPath))
            {
                string configContents = File.ReadAllText(configPath);
                if (!string.IsNullOrEmpty(configContents))
                {
                    Match match = OriginSearch.Match(configContents);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        gitInfo.OriginUrl = match.Groups[1].Value?.Trim();
                        if (gitInfo.OriginUrl != null && gitInfo.OriginUrl.Length > 0)
                            gitInfo.GitUser = GetGitHubUser(gitInfo.OriginUrl);
                    }
                }
            }
            return success;
        }

        /// <summary>
        /// Attempts to retrieve the git commit hash by reading git files.
        /// </summary>
        /// <param name="gitPaths"></param>
        /// <param name="gitInfo"></param>
        /// <returns></returns>
        public static bool TryGetCommitManual(string[] gitPaths, out GitInfo gitInfo)
        {
            gitInfo = new GitInfo();
            for (int i = 0; i < gitPaths.Length; i++)
            {
                if (TryGetCommitManual(gitPaths[i], out GitInfo retVal))
                {
                    gitInfo = retVal;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Name of the directory with git files.
        /// </summary>
        protected string GitDirectory = ".git";

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>true if successful</returns>
        public override bool Execute()
        {
            if (this.BuildEngine != null)
                Logger = new LogWrapper(Log, GetType().Name);
            else
                Logger = new MockTaskLogger(GetType().Name);
            if (GitRunner == null)
                GitRunner = new GitCommandRunner(ProjectDir);
            CommitHash = "local";
            string errorCode = null;
            string[] gitPaths = new string[]{
                        Path.GetFullPath(Path.Combine(ProjectDir, GitDirectory)),
                        Path.GetFullPath(Path.Combine(ProjectDir, "..", GitDirectory))
                    };
            try
            {
                string commitHash = null;
                if (!NoGit && TryGetGitCommit(GitRunner, Logger, out commitHash) && commitHash.Length > 0)
                {
                    CommitHash = commitHash.Substring(0, Math.Min(commitHash.Length, HashLength));
                    if (!SkipStatus)
                    {
                        GitInfo gitStatus = GetGitStatus(GitRunner, Logger);
                        if (!string.IsNullOrWhiteSpace(gitStatus.Branch))
                            Branch = gitStatus.Branch;
                        if (!string.IsNullOrWhiteSpace(gitStatus.Modified))
                            Modified = gitStatus.Modified;
                        if (!string.IsNullOrWhiteSpace(gitStatus.OriginUrl))
                            OriginUrl = gitStatus.OriginUrl;
                        if (!string.IsNullOrWhiteSpace(gitStatus.GitUser))
                            GitUser = gitStatus.GitUser;
                        IsPullRequest = gitStatus.IsPullRequest;
                    }
                    //if (string.IsNullOrWhiteSpace(Branch))
                    //{
                    //    if (TryGetCommitManual(gitPaths, out GitInfo manualInfo))
                    //    {
                    //        Branch = manualInfo.Branch;
                    //    }
                    //}
                }
                else
                {
                    if (TryGetCommitManual(gitPaths, out GitInfo gitInfo))
                    {
                        commitHash = gitInfo.CommitHash;
                        if (commitHash.Length > 0)
                            CommitHash = commitHash
                                .Substring(0,
                                  Math.Min(commitHash.Length, HashLength));
                        if (!string.IsNullOrWhiteSpace(gitInfo.Branch))
                            Branch = gitInfo.Branch;
                        if (!string.IsNullOrWhiteSpace(gitInfo.Modified))
                            Modified = gitInfo.Modified;
                        if (!string.IsNullOrWhiteSpace(gitInfo.OriginUrl))
                            OriginUrl = gitInfo.OriginUrl;
                        if (!string.IsNullOrWhiteSpace(gitInfo.GitUser))
                            GitUser = gitInfo.GitUser;
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(errorCode))
                    errorCode = MessageCodes.GetCommitInfo.GitFailed;
                if (BuildEngine != null)
                {
                    int line = BuildEngine.LineNumberOfTaskNode;
                    int column = BuildEngine.ColumnNumberOfTaskNode;
                    Logger.LogMessage(null, errorCode, null, BuildEngine.ProjectFileOfTaskNode, line, column, line, column,
                        MessageImportance.High, $"Error in {GetType().Name}: {ex.Message}");
                }
                else
                {
                    Logger.LogMessage(null, errorCode, null, null, 0, 0, 0, 0,
                        MessageImportance.High, $"Error in {GetType().Name}: {ex.Message}");
                }
            }
#pragma warning restore CA1031 // Do not catch general exception types
            if (CommitHash == "local")
            {
                if (BuildEngine != null)
                {
                    errorCode = MessageCodes.GetCommitInfo.GitNoRepository;
                    int line = BuildEngine.LineNumberOfTaskNode;
                    int column = BuildEngine.ColumnNumberOfTaskNode;
                    Logger.LogMessage(null, errorCode, null, BuildEngine.ProjectFileOfTaskNode, line, column, line, column,
                        MessageImportance.High, "Project does not appear to be in a git repository.");
                }
                else
                    Logger.LogMessage(null, errorCode, null, null, 0, 0, 0, 0,
                        MessageImportance.High, "Project does not appear to be in a git repository.");
            }
            return true;
        }

        /// <summary>
        /// Parses the GitHub username from a URL.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static string GetGitHubUser(string url)
        {
            string user = null;
            string[] parts = url.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            bool baseFound = false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (baseFound)
                {
                    user = parts[i];
                    break;
                }
                if (parts[i].ToUpper().Contains("GITHUB.COM"))
                    baseFound = true;
            }
            return user;
        }
    }

    /// <summary>
    /// Container for data of a git repository.
    /// </summary>
    public struct GitInfo
    {
        /// <summary>
        /// Current commit hash.
        /// </summary>
        public string CommitHash;
        /// <summary>
        /// Current branch of the repository.
        /// </summary>
        public string Branch;
        /// <summary>
        /// True if this branch appears to be a pull request.
        /// </summary>
        public bool IsPullRequest;
        /// <summary>
        /// 'Modified' if the repository is modified, 'Unmodified' if it's not.
        /// Null/Empty string if undetermined.
        /// </summary>
        public string Modified;
        /// <summary>
        /// URL for the repository's origin.
        /// </summary>
        public string OriginUrl;
        /// <summary>
        /// Username of the repository's owner.
        /// </summary>
        public string GitUser;
    }
}
