using System.Runtime.ExceptionServices;
using System.Text;
using NUnit.Framework;
using TSParser.DictionariesData;

namespace TSParser.Tests;

[TestFixture]
public class DictionariesTests
{
    [Test]
    [NonParallelizable]
    public void Invalid_utf8_probe_uses_non_throwing_validation()
    {
        var decoderFallbackExceptions = 0;
        EventHandler<FirstChanceExceptionEventArgs> handler = (_, args) =>
        {
            if (args.Exception is DecoderFallbackException)
                decoderFallbackExceptions++;
        };

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try
        {
            var decoded = Dictionaries.BytesToStringPreferUtf8Cyrillic([0xD0]);

            Assert.That(decoded, Is.Not.Empty);
            Assert.That(decoderFallbackExceptions, Is.Zero);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }
    }
}
