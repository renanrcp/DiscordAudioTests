// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace DiscordAudioTests.Logger;

public sealed class CustomFormatter : ConsoleFormatter, IDisposable
{
    public const string DefaultTimestampFormatter = "MMM d - HH:mm:ss";

    private readonly IDisposable _optionsReloadToken;
    private CustomFormatterOptions _formatterOptions;

    public CustomFormatter(IOptionsMonitor<CustomFormatterOptions> options) : base(nameof(CustomFormatter))
    {
        (_optionsReloadToken, _formatterOptions) = (options.OnChange(ReloadLoggerOptions), options.CurrentValue);
    }

    private bool UseUtcTimestamp => _formatterOptions.UseUtcTimestamp;

    private string TimestampFormat => _formatterOptions.TimestampFormat ?? DefaultTimestampFormatter;

    private bool IncludeScopes => _formatterOptions.IncludeScopes;

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
    {
        var message = logEntry.Formatter(logEntry.State, logEntry.Exception);

        if (logEntry.Exception == null && message == null)
        {
            return;
        }

        var eventId = logEntry.EventId.Id;
        var category = logEntry.Category;
        var logLevel = logEntry.LogLevel;
        var exception = logEntry.Exception;

        var now = UseUtcTimestamp
            ? DateTimeOffset.UtcNow
            : DateTimeOffset.Now;

        var timestampFormat = TimestampFormat;

        WriteTimestamp(textWriter, now, timestampFormat);
        WriteLogLevel(textWriter, logLevel);
        WriteCategory(textWriter, category);
        WriteEventId(textWriter, eventId);

        textWriter.WriteLine();

        WriteScopes(textWriter, IncludeScopes, scopeProvider);
        WriteMessage(textWriter, message);
        WriteException(textWriter, exception);
    }

    private static void WriteTimestamp(TextWriter textWriter, DateTimeOffset now, string timestampFormat)
    {
        textWriter.WriteColored(now.ToString(timestampFormat, CultureInfo.InvariantCulture), Color.Plum, true);
        textWriter.Write(' ');
    }

    private static void WriteLogLevel(TextWriter textWriter, LogLevel logLevel)
    {
        var (levelAbbreviation, levelColor) = GetLogLevelFormatting(logLevel);

        textWriter.WriteColored(levelAbbreviation, levelColor, true);
        textWriter.Write(' ');
    }

    private static void WriteCategory(TextWriter textWriter, string category)
    {
        textWriter.WriteColored(category, Color.Orange, true);
    }

    private static void WriteEventId(TextWriter textWriter, int eventId)
    {
        textWriter.WriteColored(eventId, Color.WhiteSmoke, true);
    }

    private static void WriteScopes(TextWriter textWriter, bool includeScopes, IExternalScopeProvider scopeProvider)
    {
        if (includeScopes && scopeProvider != null)
        {
            scopeProvider.ForEachScope((scope, state) =>
            {
                state.WriteColored(scope, Color.LightSteelBlue, true);
                state.WriteLine();
            }, textWriter);
        }
    }

    private static void WriteMessage(TextWriter textWriter, string message)
    {
        if (!string.IsNullOrWhiteSpace(message) && !message.Equals("[null]", StringComparison.Ordinal))
        {
            textWriter.WriteColored(message, Color.WhiteSmoke, false);
            textWriter.WriteLine();
        }
    }

    private static void WriteException(TextWriter textWriter, Exception exception)
    {
        if (exception != null)
        {
            textWriter.WriteColored(exception.ToString(), Color.WhiteSmoke, false);
            textWriter.WriteLine();
        }
    }

    private static (string, Color) GetLogLevelFormatting(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Information => ("INFO", Color.MediumSeaGreen),
            LogLevel.Debug => ("DBUG", Color.MediumPurple),
            LogLevel.Trace => ("TRCE", Color.MediumPurple),
            LogLevel.Critical => ("CRIT", Color.Crimson),
            LogLevel.Error => ("EROR", Color.Crimson),
            LogLevel.Warning => ("WARN", Color.Orange),
            LogLevel.None => throw new NotImplementedException(),
            _ => ("UKNW", Color.Tomato),
        };
    }

    private void ReloadLoggerOptions(CustomFormatterOptions options)
    {
        _formatterOptions = options;
    }

    public void Dispose()
    {
        _optionsReloadToken?.Dispose();
    }
}
