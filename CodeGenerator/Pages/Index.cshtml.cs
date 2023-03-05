using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeGenerator.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public string HandlerFileName { get; set; }

        [BindProperty]
        public string HandlerFilePath { get; set; }

        [BindProperty]
        public string ServiceRequest { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            // Parse the JSON input and generate the C# classes
            var json = ServiceRequest.Trim();
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            var jsonDocument = JsonDocument.Parse(json);
            var rootElement = jsonDocument.RootElement;

            // Generate the C# code from the JSON
            var csharpCode = GenerateCSharpCode(rootElement, $"{HandlerFileName}Request");

            // Save the C# code to a file
            var fileName = $"{HandlerFileName}Handler.cs";
            var filePath = Path.Combine(HandlerFilePath, fileName);
            System.IO.File.WriteAllText(filePath, csharpCode);

            // Return a success message
            return new JsonResult(new { message = "Code generated successfully!" });
        }

        private string GenerateCSharpCode(JsonElement element, string propertyName = "Root")
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    return GenerateObjectCSharpCode(element, propertyName);
                case JsonValueKind.Array:
                    return GenerateArrayCSharpCode(element, propertyName);
                case JsonValueKind.String:
                    return $"public string {NormalizePropertyName(propertyName)} {{ get; set; }}";
                case JsonValueKind.Number:
                    return $"public double {NormalizePropertyName(propertyName)} {{ get; set; }}";
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return $"public bool {NormalizePropertyName(propertyName)} {{ get; set; }}";
                case JsonValueKind.Null:
                    return $"public object {NormalizePropertyName(propertyName)} {{ get; set; }}";
                default:
                    return "";
            }
        }

        private string GenerateObjectCSharpCode(JsonElement element, string propertyName)
        {
            var properties = element.EnumerateObject()
                .Select(x => GenerateCSharpCode(x.Value, x.Name));

            return $@"
                    public class {NormalizeClassName(propertyName)}
                    {{
                        {string.Join("\n", properties)}
                    }}
                    ";
        }

        private static string NormalizeClassName(string className)
        {
            // Remove any invalid characters from the class name
            var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { ' ' });
            className = new string(className
                .Where(c => !invalidChars.Contains(c))
                .ToArray());

            // Ensure the class name starts with a valid character
            if (!char.IsLetter(className[0]))
            {
                className = "Class" + className;
            }

            return className;
        }

        private static string NormalizePropertyName(string propertyName)
        {
            // Remove any invalid characters from the property name
            var invalidChars = new[] { ' ', '-' };
            propertyName = new string(propertyName
                .Where(c => !invalidChars.Contains(c))
                .ToArray());

            // Ensure the property name starts with a valid character
            if (!char.IsLetter(propertyName[0]))
            {
                propertyName = "Property" + propertyName;
            }

            return propertyName;
        }

        private static string GenerateArrayCSharpCode(JsonElement arrayElement, string propertyName)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"public class {NormalizeClassName(propertyName)}");
            sb.AppendLine("{");

            if (arrayElement.GetArrayLength() > 0)
            {
                var itemElement = arrayElement[0];

                if (itemElement.ValueKind == JsonValueKind.Object)
                {
                    sb.AppendLine($"    public {NormalizeClassName(propertyName)}()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        Items = new List<" + NormalizeClassName(propertyName) + "Item>();");
                    sb.AppendLine("    }");

                    sb.AppendLine();
                    sb.AppendLine($"    public List<{NormalizeClassName(propertyName)}Item> Items {{ get; set; }}");

                    sb.AppendLine();
                    sb.AppendLine("    public class " + NormalizeClassName(propertyName) + "Item");
                    sb.AppendLine("    {");

                    foreach (var objProperty in itemElement.EnumerateObject())
                    {
                        var propertyType = GetCSharpType(objProperty.Value.ValueKind);
                        var propertyNameNormalized = NormalizePropertyName(objProperty.Name);

                        sb.AppendLine($"        public {propertyType} {propertyNameNormalized} {{ get; set; }}");
                    }

                    sb.AppendLine("    }");
                }
                else
                {
                    var propertyType = GetCSharpType(itemElement.ValueKind);
                    sb.AppendLine($"    public List<{propertyType}> Items {{ get; set; }}");
                }
            }
            else
            {
                sb.AppendLine($"    public List<object> Items {{ get; set; }}");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetCSharpType(JsonValueKind valueKind)
        {
            switch (valueKind)
            {
                case JsonValueKind.String:
                    return "string";
                case JsonValueKind.Number:
                    return "double";
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return "bool";
                case JsonValueKind.Null:
                    return "object";
                default:
                    return "object";
            }
        }
    }
}