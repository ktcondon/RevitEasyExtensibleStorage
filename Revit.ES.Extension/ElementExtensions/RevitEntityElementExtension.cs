/* 
 * THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
 * PARTICULAR PURPOSE.
 * 
 */

using Autodesk.Revit.DB.ExtensibleStorage;
using Revit.ES.Extension.Attributes;

namespace Revit.ES.Extension.ElementExtensions;

public static class RevitEntityElementExtension
{
    public static void SetEntity(this Element element, IRevitEntity revitEntity)
    {
        if (element is null)
            return;

        ISchemaCreator schemaCreator = new SchemaCreator();
        IEntityConverter entityConverter = new EntityConverter(schemaCreator);
        Entity entity = entityConverter.Convert(revitEntity);

        element.SetEntity(entity);
    }

    public static TRevitEntity GetEntity<TRevitEntity>(this Element element) where TRevitEntity : class, IRevitEntity, new()
    {
        Type revitEntityType = typeof(TRevitEntity);

        AttributeExtractor<SchemaAttribute> schemaAttributeExtractor = new();
        SchemaAttribute schemaAttribute = schemaAttributeExtractor.GetAttribute(revitEntityType);

        Schema schema = Schema.Lookup(schemaAttribute.GUID);
        if (schema == null)
            return new();

        Entity entity = element.GetEntity(schema);

        if (entity == null || !entity.IsValid())
            return new();

        ISchemaCreator schemaCreator = new SchemaCreator();
        IEntityConverter entityConverter = new EntityConverter(schemaCreator);
        TRevitEntity revitEntity = entityConverter.Convert<TRevitEntity>(entity);

        return revitEntity;
    }

    public static bool DeleteEntity<TRevitEntity>(this Element element) where TRevitEntity : class, IRevitEntity
    {
        Type revitEntityType = typeof(TRevitEntity);

        AttributeExtractor<SchemaAttribute> schemaAttributeExtractor = new();
        SchemaAttribute schemaAttribute = schemaAttributeExtractor.GetAttribute(revitEntityType);

        Schema schema = Schema.Lookup(schemaAttribute.GUID);
        if (schema == null)
            return false;

        return element.DeleteEntity(schema);
    }
}

public static class EntityExtension
{
    public static void SetWrapper<T>(this Entity entity, Field field, IList<T> value)
    {
        entity.Set(field, value);
    }

    public static void SetWrapper<T>(this Entity entity, Field field, IList<T> value, ForgeTypeId displayUnitType)
    {
        entity.Set(field, value, displayUnitType);
    }

    public static void SetWrapper<TKey, TValue>(this Entity entity, Field field, IDictionary<TKey, TValue> value)
    {
        entity.Set(field, value);
    }

    public static void SetWrapper<TKey, TValue>(this Entity entity, Field field, IDictionary<TKey, TValue> value, ForgeTypeId displayUnitType)
    {
        entity.Set(field, value, displayUnitType);
    }
}
