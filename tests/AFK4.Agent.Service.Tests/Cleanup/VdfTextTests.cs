using AFK4.Agent.Service.Cleanup;

namespace AFK4.Agent.Service.Tests.Cleanup;

public sealed class VdfTextTests
{
    private const string Config = """
        "InstallConfigStore"
        {
        	"Software"
        	{
        		"Valve"
        		{
        			"Steam"
        			{
        				"CellID"		"52"
        				"ConnectCache"
        				{
        					"a1b2c3"		"0200000062616e61{}6e61"
        				}
        				"BaseInstallFolder_1"		"D:\\Games\\Steam"
        			}
        		}
        	}
        }
        """;

    // Вход следующего игрока держится на ConnectCache; всё остальное в файле — настройки клиента и
    // папки библиотек, и их надо оставить как были.
    [Fact]
    public void RemovesTheLoginCache_AndKeepsEverythingElse()
    {
        var trimmed = VdfText.RemoveBlocks(Config, "ConnectCache", out var removed);

        Assert.Equal(1, removed);
        Assert.DoesNotContain("ConnectCache", trimmed, StringComparison.Ordinal);
        Assert.DoesNotContain("a1b2c3", trimmed, StringComparison.Ordinal);
        Assert.Contains("\"CellID\"\t\t\"52\"", trimmed, StringComparison.Ordinal);
        Assert.Contains("\"BaseInstallFolder_1\"\t\t\"D:\\\\Games\\\\Steam\"", trimmed, StringComparison.Ordinal);
        // Файл остаётся цельным: скобки сходятся, и Steam его прочитает.
        Assert.Equal(trimmed.Count(character => character == '{'), trimmed.Count(character => character == '}'));
        Assert.Equal(Config.Count(character => character == '{') - 2, trimmed.Count(character => character == '{'));
    }

    [Fact]
    public void WithoutTheBlock_TheFileIsUntouched()
    {
        const string text = "\"Steam\"\n{\n\t\"CellID\"\t\t\"52\"\n}\n";

        Assert.Equal(text, VdfText.RemoveBlocks(text, "ConnectCache", out var removed));
        Assert.Equal(0, removed);
    }

    // Ключ со значением-строкой — не блок: вырезать его значило бы сломать файл.
    [Fact]
    public void AKeyWithAStringValue_IsNotABlock()
    {
        const string text = "\"Steam\"\n{\n\t\"ConnectCache\"\t\t\"x\"\n}\n";

        Assert.Equal(text, VdfText.RemoveBlocks(text, "ConnectCache", out var removed));
        Assert.Equal(0, removed);
    }

    [Fact]
    public void BracesInsideQuotes_DoNotEndTheBlockEarly()
    {
        const string text = "\"A\"\n{\n\t\"ConnectCache\"\n\t{\n\t\t\"k\"\t\"}{\"\n\t}\n\t\"Keep\"\t\"1\"\n}\n";

        var trimmed = VdfText.RemoveBlocks(text, "connectcache", out var removed);

        Assert.Equal(1, removed);
        Assert.Equal("\"A\"\n{\n\t\"Keep\"\t\"1\"\n}\n", trimmed);
    }
}
