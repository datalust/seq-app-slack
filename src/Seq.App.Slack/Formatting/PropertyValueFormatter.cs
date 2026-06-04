using Newtonsoft.Json;

namespace Seq.App.Slack.Formatting
{
    class PropertyValueFormatter
    {
        private readonly int? _maxPropertyLength;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public PropertyValueFormatter(int? maxPropertyLength)
        {
            _maxPropertyLength = maxPropertyLength;
        }

        public string ConvertPropertyValueToString(object? propertyValue)
        {
            if (propertyValue == null)
                return string.Empty;

            var t = propertyValue.GetType();
            var isDict = t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
            var result = isDict ? JsonConvert.SerializeObject(propertyValue, JsonSettings) : propertyValue.ToString();

            if (_maxPropertyLength.HasValue)
            {
                if (result?.Length > _maxPropertyLength)
                {
                    result = result[.._maxPropertyLength.Value] + "...";
                }
            }

            return result ?? "";
        }
    }
}