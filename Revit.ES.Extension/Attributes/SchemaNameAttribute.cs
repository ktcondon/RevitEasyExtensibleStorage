/* 
 * THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
 * PARTICULAR PURPOSE.
 * 
 */

using Autodesk.Revit.DB.ExtensibleStorage;

namespace Revit.ES.Extension.Attributes;


[AttributeUsage(AttributeTargets.Class)]
public class SchemaAttribute : Attribute
{
    private readonly string schemaName;
    private readonly Guid guid;

    public SchemaAttribute(string guid, string schemaName)
    {
        this.schemaName = schemaName;
        this.guid = new Guid(guid);
    }

    public string SchemaName
    {
        get { return schemaName; }
    }

    public Guid ApplicationGUID { get; set; }

    public string Documentation { get; set; }

    public Guid GUID
    {
        get { return guid; }
    }

    public AccessLevel ReadAccessLevel { get; set; }

    public AccessLevel WriteAccessLevel { get; set; }

    public string VendorId { get; set; }
}
