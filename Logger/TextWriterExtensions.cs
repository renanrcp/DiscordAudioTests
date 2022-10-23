// Licensed to the NextAudio under one or more agreements.
// NextAudio licenses this file to you under the MIT license.

using System;
using System.Drawing;
using System.IO;
using ANSIConsole;

namespace DiscordAudioTests.Logger;

public static class TextWriterExtensions
{
    private static readonly bool IsAndroidOrAppleMobile = OperatingSystem.IsAndroid() ||
                                                      OperatingSystem.IsTvOS() ||
                                                      OperatingSystem.IsIOS();
    private static volatile int s_emitAnsiColorCodes = -1;

    private static bool EmitAnsiColorCodes
    {
        get
        {
            var emitAnsiColorCodes = s_emitAnsiColorCodes;
            if (emitAnsiColorCodes != -1)
            {
                return Convert.ToBoolean(emitAnsiColorCodes);
            }

            var enabled = !Console.IsOutputRedirected;

            if (enabled)
            {
                enabled = Environment.GetEnvironmentVariable("NO_COLOR") == null;
            }
            else
            {
                var envVar = Environment.GetEnvironmentVariable("DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION");
                enabled = envVar is not null && (envVar == "1" || envVar.Equals("true", StringComparison.OrdinalIgnoreCase));
            }

            s_emitAnsiColorCodes = Convert.ToInt32(enabled);
            return enabled;
        }
    }

    private static readonly bool IsColorEnabled = EmitAnsiColorCodes && !IsAndroidOrAppleMobile;

    public static void WriteColored(this TextWriter textWriter, string text, Color color, bool betweenBrackets)
    {
        if (betweenBrackets)
        {
            textWriter.Write("[".WriteColor(Color.White));
        }

        textWriter.Write(text.Color(color));

        if (betweenBrackets)
        {
            textWriter.Write("]".WriteColor(Color.White));
        }
    }

    public static void WriteColored(this TextWriter textWriter, object value, Color color, bool betweenBrackets)
    {
        if (value == null)
        {
            return;
        }

        if (value is IFormattable f)
        {
            textWriter.WriteColored(f.ToString(null, textWriter.FormatProvider), color, betweenBrackets);
            return;
        }

        textWriter.WriteColored(value.ToString(), color, betweenBrackets);
    }

    private static string WriteColor(this string text, Color color)
    {
        return IsColorEnabled ? text.Color(color).ToString() : text;
    }
}
