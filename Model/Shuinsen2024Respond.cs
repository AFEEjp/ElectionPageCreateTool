using System.Text.Json.Serialization;

namespace ElectionPageCreateTool.Model
{
    public class Shuinsen2024Respond
    {
        [JsonPropertyName("名前")]
        public string Name { get; set; }

        [JsonPropertyName("なまえふりがな")]
        public string NameFurigana { get; set; }

        [JsonPropertyName("誕生日")]
        public string Birthdate { get; set; }

        [JsonPropertyName("選挙区・比例ブロック")]
        public string Constituency { get; set; }

        [JsonPropertyName("所属政党名")]
        public string PartyName { get; set; }

        [JsonPropertyName("【Q1】実在しない児童（キャラクター）を描写した、過激な性的・暴力等の表現を含むマンガ・アニメ・ゲーム等について、成人が所持・提供・製造すること等を法令で規制するべきと考えますか？")]
        public string Q1Answer { get; set; }

        [JsonPropertyName("Q1の選択肢を選んだ理由を100文字以内で回答してください。")]
        public string Q1Reason { get; set; }

        [JsonPropertyName("【Q2】表現の自由の観点から問題があると考えられるものを以下の選択肢からお選びください（複数回答可）")]
        public string Q2Answer { get; set; }

        [JsonPropertyName("Q2の選択肢を選んだ理由を100文字以内で回答してください。")]
        public string Q2Reason { get; set; }

        [JsonPropertyName("好きなマンガ・アニメ・ゲームがありましたら教えてください（最大3つ、100文字以内）")]
        public string FavoriteMangaAnimeGame { get; set; }
    }
}
