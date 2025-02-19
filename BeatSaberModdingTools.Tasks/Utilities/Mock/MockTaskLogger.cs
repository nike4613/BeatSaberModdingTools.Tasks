﻿using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;

namespace BeatSaberModdingTools.Tasks.Utilities.Mock
{
    /// <summary>
    /// A mock logger that implements <see cref="ITaskLogger"/> for unit testing.
    /// </summary>
    public class MockTaskLogger : LoggerBase
    {
        /// <summary>
        /// List of log entries created by this instance.
        /// </summary>
        public List<MockLogEntry> LogEntries = new List<MockLogEntry>();

        /// <summary>
        /// Creates a new <see cref="MockTaskLogger"/> with the given task name.
        /// </summary>
        /// <param name="taskName"></param>
        public MockTaskLogger(string taskName)
            : base(taskName) { }

        /// <inheritdoc/>
        public override void LogError(string subcategory, string errorCode, string helpKeyword, string file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber, string message, params object[] messageArgs)
        {
            MockLogEntry entry = new MockLogEntry(LogEntryType.Error)
            {
                SubCategory = subcategory,
                MessageCode = errorCode,
                HelpKeyword = helpKeyword,
                File = file,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                EndLineNumber = endLineNumber,
                EndColumnNumber = endColumnNumber,
                Message = $"{TaskName}: {message}",
                MessageArgs = messageArgs,
                Importance = MessageImportance.High
            };
            LogEntries.Add(entry);
            Console.WriteLine(entry);
        }

        /// <inheritdoc/>
        public override void LogError(string message, params object[] messageArgs)
        {
            MockLogEntry entry = new MockLogEntry(LogEntryType.Error)
            {
                Message = $"{TaskName}: {message}",
                MessageArgs = messageArgs,
                Importance = MessageImportance.High
            };
            LogEntries.Add(entry);
            Console.WriteLine(entry);
        }

        /// <inheritdoc/>
        public override void LogErrorFromException(Exception exception)
        {
            MockLogEntry entry = new MockLogEntry(LogEntryType.Exception)
            {
                Exception = exception,
                Importance = MessageImportance.High
            };
            LogEntries.Add(entry);
            Console.WriteLine(entry);
        }

        /// <inheritdoc/>
        public override void LogMessage(MessageImportance importance, string message, params object[] messageArgs)
        {
            MockLogEntry entry = new MockLogEntry(LogEntryType.Message)
            {
                Message = $"{TaskName}: {message}",
                MessageArgs = messageArgs,
                Importance = importance
            };
            LogEntries.Add(entry);
            Console.WriteLine(entry);
        }

        /// <inheritdoc/>
        public override void LogMessage(string subcategory, string code, string helpKeyword, string file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber, MessageImportance messageImportance, string message, params object[] messageArgs)
        {
            MockLogEntry entry = new MockLogEntry(LogEntryType.Message)
            {
                SubCategory = subcategory,
                MessageCode = code,
                HelpKeyword = helpKeyword,
                File = file,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                EndLineNumber = endLineNumber,
                EndColumnNumber = endColumnNumber,
                Message = $"{TaskName}: {message}",
                MessageArgs = messageArgs,
                Importance = MessageImportance.High
            };
            LogEntries.Add(entry);
            Console.WriteLine(entry);
        }

        /// <inheritdoc/>
        public override void LogWarning(string subcategory, string warningCode, string helpKeyword, string file, int lineNumber, int columnNumber, int endLineNumber, int endColumnNumber, string message, params object[] messageArgs)
        {
            MockLogEntry entry = new MockLogEntry(LogEntryType.Warning)
            {
                SubCategory = subcategory,
                MessageCode = warningCode,
                HelpKeyword = helpKeyword,
                File = file,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                EndLineNumber = endLineNumber,
                EndColumnNumber = endColumnNumber,
                Message = $"{TaskName}: {message}",
                MessageArgs = messageArgs,
                Importance = MessageImportance.High
            };
            LogEntries.Add(entry);
            Console.WriteLine(entry);
        }

        /// <inheritdoc/>
        public override void LogWarning(string message, params object[] messageArgs)
        {
            MockLogEntry entry = new MockLogEntry(LogEntryType.Warning)
            {
                Message = $"{TaskName}: {message}",
                MessageArgs = messageArgs,
                Importance = MessageImportance.High
            };
            LogEntries.Add(entry);
            Console.WriteLine(entry);
        }


        /// <inheritdoc/>
        public override void Log(LogMessageLevel level, string message, params object[] messageArgs)
        {
            switch (level)
            {
                case LogMessageLevel.Message:
                    LogMessage(MessageImportance.High, message, messageArgs);
                    break;
                case LogMessageLevel.Warning:
                    LogWarning(message, messageArgs);
                    break;
                case LogMessageLevel.Error:
                    LogError(message, messageArgs);
                    break;
                default:
                    LogMessage(MessageImportance.High, message, messageArgs);
                    break;
            }
        }
    }

    /// <summary>
    /// Stores log entry data.
    /// </summary>
    public struct MockLogEntry
    {
        /// <summary>
        /// Log entry SubCategory.
        /// </summary>
        public string SubCategory;
        /// <summary>
        /// Log entry MessageCode.
        /// </summary>
        public string MessageCode;
        /// <summary>
        /// Log entry HelpKeyword.
        /// </summary>
        public string HelpKeyword;
        /// <summary>
        /// Log entry File name.
        /// </summary>
        public string File;
        /// <summary>
        /// Log entry line number.
        /// </summary>
        public int LineNumber;
        /// <summary>
        /// Log entry column number.
        /// </summary>
        public int ColumnNumber;
        /// <summary>
        /// Log entry end line number.
        /// </summary>
        public int EndLineNumber;
        /// <summary>
        /// Log entry end column number.
        /// </summary>
        public int EndColumnNumber;
        /// <summary>
        /// Log entry Message.
        /// </summary>
        public string Message;
        /// <summary>
        /// Log entry Message args.
        /// </summary>
        public object[] MessageArgs;
        /// <summary>
        /// Log entry Exception.
        /// </summary>
        public Exception Exception;
        /// <summary>
        /// Log entry Importance.
        /// </summary>
        public MessageImportance Importance;
        /// <summary>
        /// Log entry type.
        /// </summary>
        public LogEntryType EntryType;

        /// <summary>
        /// Creates a new <see cref="MockLogEntry"/> of the given type.
        /// </summary>
        /// <param name="entryType"></param>
        public MockLogEntry(LogEntryType entryType)
        {
            SubCategory = null;
            MessageCode = null;
            HelpKeyword = null;
            File = null;
            LineNumber = 0;
            ColumnNumber = 0;
            EndLineNumber = 0;
            EndColumnNumber = 0;
            Message = null;
            MessageArgs = Array.Empty<object>();
            Exception = null;
            Importance = MessageImportance.High;
            EntryType = entryType;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (EntryType == LogEntryType.Exception)
                return Exception.Message;
            string message = Message;
            for (int i = 0; i < (MessageArgs?.Length ?? 0); i++)
            {
                message = message.Replace($"{{{i}}}", MessageArgs[i]?.ToString() ?? string.Empty);
            }
            return message;
        }
    }
    /// <summary>
    /// Log entry type.
    /// </summary>
    public enum LogEntryType
    {
        /// <summary>
        /// Message
        /// </summary>
        Message,
        /// <summary>
        /// Warning
        /// </summary>
        Warning,
        /// <summary>
        /// Error
        /// </summary>
        Error,
        /// <summary>
        /// Exception
        /// </summary>
        Exception
    }
}
