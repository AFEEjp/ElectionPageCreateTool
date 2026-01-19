namespace ElectionPageCreateTool.Attributes
{
    /// <summary>
    /// Excelのヘッダー名とプロパティのマッピングを定義する属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ExcelColumnAttribute : Attribute
    {
        /// <summary>
        /// Excelのヘッダー名
        /// </summary>
        public string HeaderName { get; }

        /// <summary>
        /// Upsert時のキーカラムとして使用するかどうか
        /// </summary>
        public bool IsUpsertKey { get; set; } = false;

        /// <summary>
        /// ふりがなカラムかどうか（スペース除去して比較する）
        /// </summary>
        public bool IsFurigana { get; set; } = false;

        public ExcelColumnAttribute(string headerName)
        {
            HeaderName = headerName;
        }
    }
}
