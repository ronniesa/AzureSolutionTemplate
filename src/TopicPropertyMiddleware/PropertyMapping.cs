using System;
using System.Collections.Generic;
using System.Text;

namespace TopicPropertyMiddleware
{
    public class PropertyMapping
    {
        /// <summary>
        /// Incoming source property name
        /// </summary>
        public string SourcePropertyName { get; set; }

        /// <summary>
        /// Target property name
        /// </summary>
        public string TargetPropertyName { get; set; }

        /// <summary>
        /// Only parse it once
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static IList<PropertyMapping> Parse(string value)
        {
            var result = new List<PropertyMapping>();
            var tokens = value.Split(',');
            foreach (var token in tokens)
            {
                var sourceAndTarget = token.Split('=');
                if (sourceAndTarget.Length == 2)
                {
                    result.Add(new PropertyMapping()
                    {
                        SourcePropertyName = sourceAndTarget[0].Trim(),
                        TargetPropertyName = sourceAndTarget[1].Trim()
                    });
                }
                else
                {
                    var pm = new PropertyMapping();
                    pm.SourcePropertyName = token.Trim();
                    pm.TargetPropertyName = pm.SourcePropertyName;
                    result.Add(pm);
                }
            }

            return result;
        }
    }
}
