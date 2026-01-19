using ElectionPageCreateTool.Attributes;

namespace ElectionPageCreateTool.Model.Shuinsen2026
{
    public class Shuinsen2026Respond
    {
        [ExcelColumn("候補者名")]
        public string Name { get; set; } = string.Empty;

        [ExcelColumn("候補者名（ふりがな）", IsUpsertKey = true, IsFurigana = true)]
        public string NameFurigana { get; set; } = string.Empty;

        [ExcelColumn("候補者の生年月日")]
        public string Birthdate { get; set; } = string.Empty;

        [ExcelColumn("選挙区・比例ブロック", IsUpsertKey = true)]
        public string Constituency { get; set; } = string.Empty;

        [ExcelColumn("所属政党名", IsUpsertKey = true)]
        public string PartyName { get; set; } = string.Empty;

        [ExcelColumn("【Q1】未成年に見えるキャラクターに対する性的・暴力的な表現があるマンガ・アニメ・ゲームなどの作品の創作・公開・所持について、どのような対応が望ましいと考えますか？")]
        public string Q1Answer { get; set; } = string.Empty;

        [ExcelColumn("Q1の選択肢を選んだ理由を100文字以内で回答してください")]
        public string Q1Reason { get; set; } = string.Empty;

        [ExcelColumn("【Q2】以下の項目のうち、「表現の自由を必要以上に損ねている」または「表現の自由に過度に抵触する可能性がある」と思うものをすべてお選びください（複数選択可）")]
        public string Q2Answer { get; set; } = string.Empty;

        [ExcelColumn("Q2の選択肢を選んだ理由を100文字以内で回答してください")]
        public string Q2Reason { get; set; } = string.Empty;

        [ExcelColumn("【Q3】マンガ・アニメ・ゲームなど好きなエンタメ作品があれば、具体的な作品名を教えてください（最大3つ、100文字以内）")]
        public string FavoriteMangaAnimeGame { get; set; } = string.Empty;

        [ExcelColumn("当落")]
        public string Result { get; set; } = string.Empty;
    }
}
