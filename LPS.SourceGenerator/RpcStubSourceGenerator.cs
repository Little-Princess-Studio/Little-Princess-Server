namespace LPS.CodeGenerator;

using System.Linq;
using Microsoft.CodeAnalysis;
using Scriban;


[Generator]
public class RpcStubSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classTemplate = context.AdditionalTextsProvider
            .Where(text => text.Path == "templates\\RpcStubImpl.template")
            .Select((text, token) => text.GetText(token)?.ToString())!.Collect<string>();
        
        context.RegisterSourceOutput(context.CompilationProvider.Combine(classTemplate), (productionContext, source) =>
        {
            var additionalTexts = source.Right;
            var templateString = additionalTexts.First()!;

            var template = Template.Parse(templateString);

            var code = template.Render(new
            {
                stubInterfaceName = "TestInterfaceName",
                entityType = "BaseEntity",
                methodsToImpl = new []
                {
                    new
                    {
                        noReturn = "false",
                        returnType = "Task",
                        name = "Test",
                        parameters = new []
                        {
                            "string",
                            "int",
                        },
                    },
                    new
                    {
                        noReturn = "true",
                        returnType = "ValueTask<bool>",
                        name = "Test",
                        parameters = new []
                        {
                            "string",
                            "int",
                            "bool"
                        },
                    },
                }
            });

            productionContext.AddSource("test.g.cs", code);
        });
    }
}