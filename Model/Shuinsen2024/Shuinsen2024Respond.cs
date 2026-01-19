using ElectionPageCreateTool.Attributes;

namespace ElectionPageCreateTool.Model.Shuinsen2024
{
    public class Shuinsen2024Respond
    {
        [ExcelColumn("名前")]
        public string Name { get; set; } = string.Empty;

        [ExcelColumn("なまえふりがな", IsUpsertKey = true, IsFurigana = true)]
        public string NameFurigana { get; set; } = string.Empty;

        [ExcelColumn("誕生日")]
        public string Birthdate { get; set; } = string.Empty;

        [ExcelColumn("選挙区・比例ブロック", IsUpsertKey = true)]
        public string Constituency { get; set; } = string.Empty;

        [ExcelColumn("所属政党名", IsUpsertKey = true)]
        public string PartyName { get; set; } = string.Empty;

        [ExcelColumn("【Q1】実在しない児童（キャラクター）を描写した、過激な性的・暴力等の表現を含むマンガ・アニメ・ゲーム等について、成人が所持・提供・製造すること等を法令で規制するべきと考えますか？")]
        public string Q1Answer { get; set; } = string.Empty;

        [ExcelColumn("Q1の選択肢を選んだ理由を100文字以内で回答してください。")]
        public string Q1Reason { get; set; } = string.Empty;

        [ExcelColumn("【Q2】表現の自由の観点から問題があると考えられるものを以下の選択肢からお選びください（複数回答可）")]
        public string Q2Answer { get; set; } = string.Empty;

        [ExcelColumn("Q2の選択肢を選んだ理由を100文字以内で回答してください。")]
        public string Q2Reason { get; set; } = string.Empty;

        [ExcelColumn("好きなマンガ・アニメ・ゲームがありましたら教えてください（最大3つ、100文字以内）")]
        public string FavoriteMangaAnimeGame { get; set; } = string.Empty;

        [ExcelColumn("当落")]
        public string Result { get; set; } = string.Empty;
    }
}
