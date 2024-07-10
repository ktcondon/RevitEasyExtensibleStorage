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
using System.Text;

namespace Revit.ES.Extension;

public interface IFieldFactory
{
    FieldBuilder CreateField(SchemaBuilder schemaBuilder, PropertyInfo propertyInfo);
}

internal class FieldFactory : IFieldFactory
{
    public FieldBuilder CreateField(SchemaBuilder schemaBuilder, PropertyInfo propertyInfo)
    {
        IFieldFactory fieldFactory = null;

        Type fieldType = propertyInfo.PropertyType;

        /* Check whether fieldType is generic or not.
         * Only IList<> and IDictionary are supported.            
         */
        if (fieldType.IsGenericType)
        {
            foreach (Type interfaceType in fieldType.GetInterfaces())
            {
                if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IList<>))
                {
                    fieldFactory = new ArrayFieldCreator();
                    break;
                }
                else if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    fieldFactory = new MapFieldCreator();
                    break;
                }
            }

            if (fieldFactory == null)
            {
                StringBuilder sb = new();
                sb.AppendLine(string.Format("Type {0} does not supported.", fieldType));
                sb.AppendLine("Only IList<T> and IDictionary<TKey, TValue> generic types are supproted");

                throw new NotSupportedException(sb.ToString());
            }
        }
        else
        {
            fieldFactory = new SimpleFieldCreator();
        }

        return fieldFactory.CreateField(schemaBuilder, propertyInfo);
    }
}

internal class SimpleFieldCreator : IFieldFactory
{
    public FieldBuilder CreateField(SchemaBuilder schemaBuilder, PropertyInfo propertyInfo)
    {
        FieldBuilder fieldBuilder;

        Type iRevitEntity = propertyInfo.PropertyType.GetInterface("IRevitEntity");

        if (iRevitEntity != null)
        {
            AttributeExtractor<SchemaAttribute> schemaAttributeExtractor = new();
            SchemaAttribute subSchemaAttribute = schemaAttributeExtractor.GetAttribute(propertyInfo.PropertyType);

            fieldBuilder = schemaBuilder.AddSimpleField(propertyInfo.Name, typeof(Entity));
            fieldBuilder.SetSubSchemaGUID(subSchemaAttribute.GUID);
        }
        else
        {
            fieldBuilder = schemaBuilder.AddSimpleField(propertyInfo.Name, propertyInfo.PropertyType);
        }

        return fieldBuilder;
    }
}

internal class ArrayFieldCreator : IFieldFactory
{
    public FieldBuilder CreateField(SchemaBuilder schemaBuilder, PropertyInfo propertyInfo)
    {
        FieldBuilder fieldBuilder;

        // Check whether generic type is subentity or not
        Type genericType = propertyInfo.PropertyType.GetGenericArguments()[0];

        Type iRevitEntity = genericType.GetInterface("IRevitEntity");

        if (iRevitEntity != null)
        {
            AttributeExtractor<SchemaAttribute> schemaAttributeExtractor = new();
            SchemaAttribute subSchemaAttribute = schemaAttributeExtractor.GetAttribute(genericType);

            fieldBuilder = schemaBuilder.AddArrayField(propertyInfo.Name, typeof(Entity));
            fieldBuilder.SetSubSchemaGUID(subSchemaAttribute.GUID);
        }
        else
        {
            fieldBuilder = schemaBuilder.AddArrayField(propertyInfo.Name, genericType);
        }

        return fieldBuilder;
    }
}

internal class MapFieldCreator : IFieldFactory
{
    public FieldBuilder CreateField(SchemaBuilder schemaBuilder, PropertyInfo propertyInfo)
    {
        FieldBuilder fieldBuilder;

        Type genericKeyType = propertyInfo.PropertyType.GetGenericArguments()[0];
        Type genericValueType = propertyInfo.PropertyType.GetGenericArguments()[1];

        if (genericValueType.GetInterface("IRevitEntity") != null)
        {
            AttributeExtractor<SchemaAttribute> schemaAttributeExtractor = new();
            SchemaAttribute subSchemaAttribute = schemaAttributeExtractor.GetAttribute(genericValueType);

            fieldBuilder = schemaBuilder.AddMapField(propertyInfo.Name, genericKeyType, typeof(Entity));
            fieldBuilder.SetSubSchemaGUID(subSchemaAttribute.GUID);
        }
        else
        {
            fieldBuilder = schemaBuilder.AddMapField(propertyInfo.Name, genericKeyType, genericValueType);
        }

        return fieldBuilder;
    }
}
