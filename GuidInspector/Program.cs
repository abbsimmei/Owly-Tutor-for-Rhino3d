
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var path = Path.GetFullPath(
    @"C:\Program Files\Rhino 9\Plug-ins\RhinoAI\net8.0\RhinoAI.rhp"
);

using var stream = File.OpenRead(path);
using var pe = new PEReader(stream);

var reader = pe.GetMetadataReader();
var assembly = reader.GetAssemblyDefinition();

foreach (var handle in assembly.GetCustomAttributes())
{
    var attribute = reader.GetCustomAttribute(handle);
    var constructor = attribute.Constructor;

    if (constructor.Kind != HandleKind.MemberReference)
        continue;

    var member = reader.GetMemberReference(
        (MemberReferenceHandle)constructor
    );

    if (member.Parent.Kind != HandleKind.TypeReference)
        continue;

    var type = reader.GetTypeReference(
        (TypeReferenceHandle)member.Parent
    );

    if (reader.GetString(type.Name) != "GuidAttribute")
        continue;

    var blob = reader.GetBlobReader(attribute.Value);
    blob.ReadUInt16();

    Console.WriteLine("Compiled plugin GUID: " + blob.ReadSerializedString());
}
