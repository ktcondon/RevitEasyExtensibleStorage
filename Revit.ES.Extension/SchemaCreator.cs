/* 
 * THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
 * PARTICULAR PURPOSE.
 * 
 */

using Autodesk.Revit.DB.ExtensibleStorage;
using Revit.ES.Extension.Attributes;
using System.Reflection;

namespace Revit.ES.Extension;

/// <summary>
/// Create an Autodesk Extensible storage schema from a type
/// </summary>
public class SchemaCreator : ISchemaCreator
{
    private readonly AttributeExtractor<SchemaAttribute> schemaAttributeExtractor = new();
    private readonly AttributeExtractor<FieldAttribute> fieldAttributeExtractor = new();

    private readonly IFieldFactory fieldFactory = new FieldFactory();

    public Schema CreateSchema(Type type)
    {
        SchemaAttribute schemaAttribute = schemaAttributeExtractor.GetAttribute(type);

        Schema existingSchema = Schema.Lookup(schemaAttribute.GUID);
        if (existingSchema is not null)
            return existingSchema;
        if (existingSchema?.IsValidObject ?? false)
            return existingSchema;

        // Create a new Schema using SchemaAttribute Properties
        SchemaBuilder schemaBuilder = new(schemaAttribute.GUID);
        schemaBuilder.SetSchemaName(schemaAttribute.SchemaName);

        // Set up other schema properties if they exists
        if (schemaAttribute.ApplicationGUID != Guid.Empty)
            schemaBuilder.SetApplicationGUID(schemaAttribute.ApplicationGUID);

        if (!string.IsNullOrEmpty(schemaAttribute.Documentation))
            schemaBuilder.SetDocumentation(schemaAttribute.Documentation);

#if (RELEASE)
    if (schemaAttribute.ReadAccessLevel != default)
        schemaBuilder.SetReadAccessLevel(schemaAttribute.ReadAccessLevel);
    if (schemaAttribute.WriteAccessLevel != default)
        schemaBuilder.SetWriteAccessLevel(schemaAttribute.WriteAccessLevel);
#else
        if (schemaAttribute.ReadAccessLevel != default)
            schemaBuilder.SetReadAccessLevel(AccessLevel.Public);
        if (schemaAttribute.WriteAccessLevel != default)
            schemaBuilder.SetWriteAccessLevel(AccessLevel.Public);
#endif
        if (!string.IsNullOrEmpty(schemaAttribute.VendorId))
            schemaBuilder.SetVendorId(schemaAttribute.VendorId);

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Iterate all of the RevitEntity Properties
        foreach (PropertyInfo pi in properties)
        {
            //get the field attribute of public properties
            object[] propertyAttributes = pi.GetCustomAttributes(typeof(FieldAttribute), true);

            // if property does not have a FieldAttribute skip this property
            if (propertyAttributes.Length == 0)
                continue;

            FieldAttribute fieldAttribute = fieldAttributeExtractor.GetAttribute(pi);

            FieldBuilder fieldBuilder = fieldFactory.CreateField(schemaBuilder, pi);

            if (!string.IsNullOrEmpty(fieldAttribute.Documentation))
                fieldBuilder.SetDocumentation(fieldAttribute.Documentation);
            if (fieldBuilder is not null)
            {
                if (fieldBuilder.NeedsUnits())
                    fieldBuilder.SetSpec(new ForgeTypeId(fieldAttribute.SpecTypeId));
            }
        }

        return schemaBuilder.Finish();
    }
}
