using System.Text.Json.Serialization;

namespace ElectionPageCreateTool.Model.Saninsen2025
{
    public class Saninsen2025Respond
    {
        [JsonPropertyName("候補者名")]
        public string Name { get; set; }

        [JsonPropertyName("候補者名（ふりがな）")]
        public string NameFurigana { get; set; }

        [JsonPropertyName("候補者の生年月日")]
        public string Birthdate { get; set; }

        [JsonPropertyName("立候補する選挙区または比例代表")]
        public string Constituency { get; set; }

        [JsonPropertyName("公認を受けている政党・政治団体")]
        public string PartyName { get; set; }

        [JsonPropertyName("【Q1】実在しない児童（キャラクター）を描写した、性的・暴力等の表現を含むマンガ・アニメ・ゲーム等について、成人が所持・提供・製造すること等を法令で規制するべきと考えますか？")]
        public string Q1Answer { get; set; }

        [JsonPropertyName("Q1の選択肢を選んだ理由を100文字以内で回答してください。")]
        public string Q1Reason { get; set; }

        [JsonPropertyName("【Q2】以下の項目について「表現の自由の観点から問題がある可能性がある」とご自身が考える規制をすべてお選びください（複数選択可）")]
        public string Q2Answer { get; set; }

        [JsonPropertyName("Q2の選択肢を選んだ理由を100文字以内で回答してください。")]
        public string Q2Reason { get; set; }

        [JsonPropertyName("【Q3】好きなマンガ・アニメ・ゲームがありましたら教えてください（最大3つ、100文字以内）")]
        public string FavoriteMangaAnimeGame { get; set; }

        [JsonPropertyName("当落")]
        public string Result { get; set; } = string.Empty;
    }
}
