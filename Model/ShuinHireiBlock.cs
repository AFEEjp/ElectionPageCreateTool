using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ElectionPageCreateTool.Model
{
    public enum ShuinHireiBlock
    {
        [Display(Name = "北海道ブロック")]
        [Description("比例北海道")]
        Hokkaido,

        [Display(Name = "東北ブロック")]
        [Description("比例東北")]
        Tohoku,

        [Display(Name = "北関東ブロック")]
        [Description("比例北関東")]
        Kitakanto,

        [Display(Name = "南関東ブロック")]
        [Description("比例南関東")]
        Minamikanto,

        [Display(Name = "東京ブロック")]
        [Description("比例東京")]
        Tokyo,

        [Display(Name = "北陸信越ブロック")]
        [Description("比例北陸信越")]
        Hokurikushinetsu,

        [Display(Name = "東海ブロック")]
        [Description("比例東海")]
        Tokai,

        [Display(Name = "近畿ブロック")]
        [Description("比例近畿")]
        Kinki,

        [Display(Name = "中国ブロック")]
        [Description("比例中国")]
        Chugoku,

        [Display(Name = "四国ブロック")]
        [Description("比例四国")]
        Shikoku,

        [Display(Name = "九州ブロック")]
        [Description("比例九州")]
        Kyushu
    }
}
