using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ElectionPageCreateTool.Model.Shuinsen2026
{
    public enum ShuinHireiBlock2026
    {
        [Display(Name = "北海道ブロック")]
        [Description("北海道ブロック")]
        Hokkaido,

        [Display(Name = "東北ブロック")]
        [Description("東北ブロック")]
        Tohoku,

        [Display(Name = "北関東ブロック")]
        [Description("北関東ブロック")]
        Kitakanto,

        [Display(Name = "南関東ブロック")]
        [Description("南関東ブロック")]
        Minamikanto,

        [Display(Name = "東京ブロック")]
        [Description("東京ブロック")]
        Tokyo,

        [Display(Name = "北陸信越ブロック")]
        [Description("北陸信越ブロック")]
        Hokurikushinetsu,

        [Display(Name = "東海ブロック")]
        [Description("東海ブロック")]
        Tokai,

        [Display(Name = "近畿ブロック")]
        [Description("近畿ブロック")]
        Kinki,

        [Display(Name = "中国ブロック")]
        [Description("中国ブロック")]
        Chugoku,

        [Display(Name = "四国ブロック")]
        [Description("四国ブロック")]
        Shikoku,

        [Display(Name = "九州ブロック")]
        [Description("九州ブロック")]
        Kyushu
    }
}
