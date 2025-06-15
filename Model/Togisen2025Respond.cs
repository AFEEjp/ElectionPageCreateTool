using System.Text.Json.Serialization;

namespace ElectionPageCreateTool.Model;

public class Togisen2025Respond
{
    [JsonPropertyName("候補者名")]
    public string Name { get; set; }

    [JsonPropertyName("候補者名（フリガナ）")]
    public string Namekana { get; set; }

    [JsonPropertyName("候補者の生年月日")]
    public string Birthdate { get; set; }

    [JsonPropertyName("立候補する選挙区")]
    public string Constituency { get; set; }

    [JsonPropertyName("公認を受けている政党・政治団体")]
    public string PartyName { get; set; }

    [JsonPropertyName("【Q1】東京都青少年健全育成条例の8条指定図書（旧・不健全図書）による規制についてどう思いますか？")]
    public string Q1Answer { get; set; }

    [JsonPropertyName("Q1の選択肢を選んだ理由を100文字以内で回答してください。")]
    public string Q1Reason { get; set; }

    [JsonPropertyName("【Q2】過激なものも含めたマンガ・アニメ・ゲームの表現についてどう思いますか")]
    public string Q2Answer { get; set; }

    [JsonPropertyName("【Q3】好きなマンガ・アニメ・ゲームがありましたら教えてください（最大3つ、100文字以内）")]
    public string FavoriteMangaAnimeGame { get; set; }

    [JsonPropertyName("AFEEアンケートの3問の回答についての補足")]
    public string AdditionalInformation { get; set; }

    [JsonPropertyName("当落")]
    public string Result { get; set; } = string.Empty;
}