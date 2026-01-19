using ElectionPageCreateTool.Attributes;

namespace ElectionPageCreateTool.Model.Saninsen2025
{
    public class Saninsen2025Respond
    {
        [ExcelColumn("候補者名")]
        public string Name { get; set; } = string.Empty;

        [ExcelColumn("候補者名（ふりがな）", IsUpsertKey = true, IsFurigana = true)]
        public string NameFurigana { get; set; } = string.Empty;

        [ExcelColumn("候補者の生年月日")]
        public string Birthdate { get; set; } = string.Empty;

        [ExcelColumn("立候補する選挙区または比例代表", IsUpsertKey = true)]
        public string Constituency { get; set; } = string.Empty;

        [ExcelColumn("公認を受けている政党・政治団体", IsUpsertKey = true)]
        public string PartyName { get; set; } = string.Empty;

        [ExcelColumn("【Q1】実在しない児童（キャラクター）を描写した、性的・暴力等の表現を含むマンガ・アニメ・ゲーム等について、成人が所持・提供・製造すること等を法令で規制するべきと考えますか？")]
        public string Q1Answer { get; set; } = string.Empty;

        [ExcelColumn("Q1の選択肢を選んだ理由を100文字以内で回答してください。")]
        public string Q1Reason { get; set; } = string.Empty;

        [ExcelColumn("【Q2】以下の項目について「表現の自由の観点から問題がある可能性がある」とご自身が考える規制をすべてお選びください（複数選択可）")]
        public string Q2Answer { get; set; } = string.Empty;

        [ExcelColumn("Q2の選択肢を選んだ理由を100文字以内で回答してください。")]
        public string Q2Reason { get; set; } = string.Empty;

        [ExcelColumn("【Q3】好きなマンガ・アニメ・ゲームがありましたら教えてください（最大3つ、100文字以内）")]
        public string FavoriteMangaAnimeGame { get; set; } = string.Empty;

        [ExcelColumn("当落")]
        public string Result { get; set; } = string.Empty;
    }
}
