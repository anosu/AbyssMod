using System.Collections.Generic;
using Absf.Master;
using AbyssMod.Services;
using Project.Master.NoaMessagePack;
using Xunit;

namespace AbyssMod.Tests;

public class MasterDataTranslatorTests
{
    [Fact]
    public void DescriptionColorTranslationsApplyToBothGameRowTypes()
    {
        var translator = MasterDataTranslator.Create(
            new Dictionary<string, Dictionary<string, Dictionary<string, string>>>
            {
                ["m_description_text_colors"] = new()
                {
                    ["word"] = new() { ["Original"] = "译文" },
                },
            }
        );
        var plural = new MDescriptionTextColors { word = "Original" };
        var singular = new MDescriptionTextColor { word = "Original" };

        Assert.Equal(2, translator.TableCount);
        Assert.True(
            translator.TryTranslate(
                new Il2CppSystem.Type(nameof(MDescriptionTextColors)),
                new MasterLoadResult<MDescriptionTextColors> { Rows = new() { plural } },
                out var pluralCount
            )
        );
        Assert.True(
            translator.TryTranslate(
                new Il2CppSystem.Type(nameof(MDescriptionTextColor)),
                new MasterLoadResult<MDescriptionTextColor> { Rows = new() { singular } },
                out var singularCount
            )
        );
        Assert.Equal(1, pluralCount);
        Assert.Equal(1, singularCount);
        Assert.Equal("译文", plural.word);
        Assert.Equal("译文", singular.word);
    }

    [Fact]
    public void TranslationPreservesManifestWordingAndLeavesUnmatchedRowsAlone()
    {
        var translator = MasterDataTranslator.Create(
            new Dictionary<string, Dictionary<string, Dictionary<string, string>>>
            {
                ["m_ability_details"] = new()
                {
                    ["description"] = new() { ["Original"] = "纹章：冲击" },
                },
            }
        );
        var matched = new MAbilityDetails { description = "Original" };
        var unmatched = new MAbilityDetails { description = "Unknown" };
        var result = new MasterLoadResult<MAbilityDetails>
        {
            Rows = new() { matched, unmatched },
        };

        Assert.True(
            translator.TryTranslate(
                new Il2CppSystem.Type(nameof(MAbilityDetails)),
                result,
                out var count
            )
        );
        Assert.Equal(1, count);
        Assert.Equal("纹章：冲击", matched.description);
        Assert.Equal("Unknown", unmatched.description);
    }
}
