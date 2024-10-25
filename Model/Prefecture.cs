using ElectionPageCreateTool.Attributes;

namespace ElectionPageCreateTool.Model
{
    using System.ComponentModel;

    public enum Prefecture
    {
        [Description("北海道")]
        [ShugiinKuwari(12, "北海道")]
        Hokkaido = 1,

        [Description("青森県")]
        [ShugiinKuwari(3, "青森")]
        Aomori,

        [Description("岩手県")]
        [ShugiinKuwari(3, "岩手")]
        Iwate,

        [Description("宮城県")]
        [ShugiinKuwari(5, "宮城")]
        Miyagi,

        [Description("秋田県")]
        [ShugiinKuwari(3, "秋田")]
        Akita,

        [Description("山形県")]
        [ShugiinKuwari(3, "山形")]
        Yamagata,

        [Description("福島県")]
        [ShugiinKuwari(4, "福島")]
        Fukushima,

        [Description("茨城県")]
        [ShugiinKuwari(7, "茨城")]
        Ibaraki,

        [Description("栃木県")]
        [ShugiinKuwari(5, "栃木")]
        Tochigi,

        [Description("群馬県")]
        [ShugiinKuwari(5, "群馬")]
        Gunma,

        [Description("埼玉県")]
        [ShugiinKuwari(16, "埼玉")]
        Saitama,

        [Description("千葉県")]
        [ShugiinKuwari(14, "千葉")]
        Chiba,

        [Description("東京都")]
        [ShugiinKuwari(30, "東京")]
        Tokyo,

        [Description("神奈川県")]
        [ShugiinKuwari(20, "神奈川")]
        Kanagawa,

        [Description("新潟県")]
        [ShugiinKuwari(5, "新潟")]
        Niigata,

        [Description("富山県")]
        [ShugiinKuwari(3, "富山")]
        Toyama,

        [Description("石川県")]
        [ShugiinKuwari(3, "石川")]
        Ishikawa,

        [Description("福井県")]
        [ShugiinKuwari(2, "福井")]
        Fukui,

        [Description("山梨県")]
        [ShugiinKuwari(2, "山梨")]
        Yamanashi,

        [Description("長野県")]
        [ShugiinKuwari(5, "長野")]
        Nagano,

        [Description("岐阜県")]
        [ShugiinKuwari(5, "岐阜")]
        Gifu,

        [Description("静岡県")]
        [ShugiinKuwari(8, "静岡")]
        Shizuoka,

        [Description("愛知県")]
        [ShugiinKuwari(16, "愛知")]
        Aichi,

        [Description("三重県")]
        [ShugiinKuwari(4, "三重")]
        Mie,

        [Description("滋賀県")]
        [ShugiinKuwari(3, "滋賀")]
        Shiga,

        [Description("京都府")]
        [ShugiinKuwari(6, "京都")]
        Kyoto,

        [Description("大阪府")]
        [ShugiinKuwari(19, "大阪")]
        Osaka,

        [Description("兵庫県")]
        [ShugiinKuwari(12, "兵庫")]
        Hyogo,

        [Description("奈良県")]
        [ShugiinKuwari(3, "奈良")]
        Nara,

        [Description("和歌山県")]
        [ShugiinKuwari(2, "和歌山")]
        Wakayama,

        [Description("鳥取県")]
        [ShugiinKuwari(2, "鳥取")]
        Tottori,

        [Description("島根県")]
        [ShugiinKuwari(2, "島根")]
        Shimane,

        [Description("岡山県")]
        [ShugiinKuwari(4, "岡山")]
        Okayama,

        [Description("広島県")]
        [ShugiinKuwari(6, "広島")]
        Hiroshima,

        [Description("山口県")]
        [ShugiinKuwari(3, "山口")]
        Yamaguchi,

        [Description("徳島県")]
        [ShugiinKuwari(2, "徳島")]
        Tokushima,

        [Description("香川県")]
        [ShugiinKuwari(3, "香川")]
        Kagawa,

        [Description("愛媛県")]
        [ShugiinKuwari(3, "愛媛")]
        Ehime,

        [Description("高知県")]
        [ShugiinKuwari(2, "高知")]
        Kochi,

        [Description("福岡県")]
        [ShugiinKuwari(11, "福岡")]
        Fukuoka,

        [Description("佐賀県")]
        [ShugiinKuwari(2, "佐賀")]
        Saga,

        [Description("長崎県")]
        [ShugiinKuwari(3, "長崎")]
        Nagasaki,

        [Description("熊本県")]
        [ShugiinKuwari(4, "熊本")]
        Kumamoto,

        [Description("大分県")]
        [ShugiinKuwari(3, "大分")]
        Oita,

        [Description("宮崎県")]
        [ShugiinKuwari(3, "宮崎")]
        Miyazaki,

        [Description("鹿児島県")]
        [ShugiinKuwari(4, "鹿児島")]
        Kagoshima,

        [Description("沖縄県")]
        [ShugiinKuwari(4, "沖縄")]
        Okinawa
    }
}
