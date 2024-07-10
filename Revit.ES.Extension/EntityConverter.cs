/* 
 * THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
 * PARTICULAR PURPOSE.
 * 
 */

using Autodesk.Revit.DB.ExtensibleStorage;
using Revit.ES.Extension.Attributes;
using Revit.ES.Extension.ElementExtensions;
using System.Collections;
using System.Reflection;

namespace Revit.ES.Extension;

class EntityConverter : IEntityConverter
{

    private readonly ISchemaCreator schemaCreator;

    public EntityConverter(ISchemaCreator schemaCreator)
    {
        this.schemaCreator = schemaCreator;
    }

    #region Implementation of IEntityCreator

    /// <summary>
    /// Convert object from IRevitEntity to a ExStorage.Entity object
    /// </summary>
    /// <param name="revitEntity">IRevitEntity object to convert</param>
    /// <returns>Converted ExStorage.Entity</returns>
    public Entity Convert(IRevitEntity revitEntity)
    {
        Type entityType = revitEntity.GetType();

        //Create the schema for IRevitEntity object
        Schema schema = schemaCreator.CreateSchema(entityType);

        Entity entity = new(schema);

        /* Iterate all of the schema field and
         * get IRevitEntity object property value
         * for each field
         */
        IList<Field> schemaFields = schema.ListFields();

        foreach (Field field in schemaFields)
        {
            /*Get the property of the IRevitEntity with the
             * same name as FieldName
             */
            PropertyInfo property = entityType.GetProperty(field.FieldName);

            //Get the property value
            dynamic propertyValue = property.GetValue(revitEntity, null);

            /*We don't need to write null value to
             * the ExStorage.Entity
             * So we just skip this property
             */
            if (propertyValue == null)
                continue;

            Type propertyValueType = propertyValue.GetType();

            AttributeExtractor<FieldAttribute> fieldAttributeExtractor = new();
            FieldAttribute fieldAttribute = fieldAttributeExtractor.GetAttribute(property);

            switch (field.ContainerType)
            {
                case ContainerType.Simple:

                    propertyValue = ConvertSimpleProperty(propertyValue, field);

                    if (fieldAttribute.UnitTypeId == null)
                    {
                        entity.Set(field, propertyValue);
                    }
                    else
                    {
                        ForgeTypeId id = new(fieldAttribute.UnitTypeId);
                        entity.Set(field, propertyValue, id);
                    }

                    break;
                case ContainerType.Array:

                    /* If we have a deal with null IList or with
                     * empty IList we must skip this field.
                     */
                    if (propertyValue.Count == 0)
                        continue;

                    dynamic convertedIListFieldValue = ConvertIListProperty(propertyValue, field);

                    /* convertedArrayFieldValue is an List<T> object.
                     * Entity.Set method throws an exception if I do not pass
                     * an IList interface as value.
                     * Even if the type implements IList<T> interface
                     * With this method which do nothing except takes a
                     * IList parameter instead FieldType, it works propoerly
                     */

                    if (fieldAttribute.UnitTypeId == null)
                    {
                        EntityExtension.SetWrapper(entity, field, convertedIListFieldValue);
                    }
                    else
                    {
                        ForgeTypeId id = new(fieldAttribute.UnitTypeId);
                        EntityExtension.SetWrapper(entity, field, convertedIListFieldValue, id);
                    }

                    break;

                case ContainerType.Map:
                    dynamic convertedMapFieldValue = ConvertIDictionaryProperty(propertyValue, field);

                    if (fieldAttribute.UnitTypeId == null)
                    {
                        EntityExtension.SetWrapper(entity, field, convertedMapFieldValue);
                    }
                    else
                    {
                        ForgeTypeId id = new(fieldAttribute.UnitTypeId);

                        EntityExtension.SetWrapper(entity,
                            field,
                            convertedMapFieldValue,
                            id);
                    }
                    break;
                default:
                    throw new NotSupportedException("Unknown Field.ContainerType");
            }
        }

        return entity;
    }

    private object ConvertIDictionaryProperty(dynamic propertyValue, Field field)
    {
        Type propertyValueType = propertyValue.GetType();

        /* An ExStorage.Entity MAp field stores an IDictionary<,>
         * So, it is need to sure, that property value type
         * supports generic IDictionary<,> interface.
         * As far as I create array field only if the property type
         * implements IDictionary<,> in the SchemaCreateor, this condition is always
         * true.
         */
        bool implementIDictionaryInterface = propertyValueType
            .GetInterfaces()
            .Any(x => x.GetGenericTypeDefinition() == typeof(IDictionary<,>));

        if (!implementIDictionaryInterface)
        {
            throw new NotSupportedException("Unsupported type");
        }

        /* ExStorage.Entity supports primitive generic types, described
         * here
         * http://wikihelp.autodesk.com/Revit/enu/2013/Help/00006-API_Developer's_Guide/0135-Advanced135/0136-Storing_136/0141-Extensib141
         * And also generic type can be an ExStorage.Entity, i.e. IDictionary<T, Entity>.
         * So, need to check whether generic type is Entity or not.
         * If true, need to convert IList<IRevitEntity> to the IDictionary<T, Entity>.
         */
        if (field.ValueType == typeof(Entity))
        {
            /* Create new list
             * As property value is a IList<IRevitEntity>, I can get
             * size of the list and pass it to the new list
             */
            Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(field.KeyType, typeof(Entity));

            IDictionary mapArray = Activator.CreateInstance(dictionaryType, new object[] { propertyValue.Count }) as IDictionary;

            foreach (dynamic keyValuePair in propertyValue)
            {
                //convert each IRevitEntity to the ExStorage.Entity
                dynamic convertedEntity = Convert(keyValuePair.Value);
                mapArray.Add(keyValuePair.Key, convertedEntity);
            }

            return mapArray;
        }

        return propertyValue;
    }

    /// <summary>
    /// Convert IRevitEntity property value to the
    /// ExStorage.Entity property value
    /// </summary>
    /// <param name="propertyValue">Value of the IRevitEntity property</param>
    /// <param name="field">Field to be converted</param>
    /// <returns></returns>
    private object ConvertSimpleProperty(dynamic propertyValue, Field field)
    {
        if (field.ContainerType != ContainerType.Simple)
        {
            throw new InvalidOperationException("Field is not a simple type");
        }

        /* If field value type is Entity,
         * the Property value type is IRevitEntity type.
         * So it is need to convert IRevitEntity to the
         * ExStorageEntity
         */
        if (field.ValueType == typeof(Entity))
        {
            propertyValue = Convert(propertyValue);
        }

        return propertyValue;
    }

    /// <summary>
    /// Convert IRevitEntity property of IList type
    /// to the ExStorage.Entity type
    /// </summary>
    /// <param name="propertyValue">Value of the IRevitEntity property</param>
    /// <param name="field">Field to be converted</param>
    /// <returns></returns>
    private object ConvertIListProperty(dynamic propertyValue, Field field)
    {
        Type propertyValueType = propertyValue.GetType();

        /* An ExStorage.Entity Array field stores an IList
         * So, it is need to sure, that property value type
         * supports generic IList<> interface.
         * As far as I create array field only if the property type
         * implements IList<> in the SchemaCreateor, this condition is always
         * true.
         */
        bool implementIListInterface = propertyValueType
            .GetInterfaces()
            .Any(x => x.GetGenericTypeDefinition() == typeof(IList<>));

        if (!implementIListInterface)
        {
            throw new NotSupportedException("Unsupported type");
        }

        /* ExStorage.Entity supports primitive generic types, described
         * here
         * http://wikihelp.autodesk.com/Revit/enu/2013/Help/00006-API_Developer's_Guide/0135-Advanced135/0136-Storing_136/0141-Extensib141
         * And also generic type can be an ExStorage.Entity, i.e. IList<Entity>.
         * So, need to check whether generic type is Entity or not.
         * If true, need to convert IList<IRevitEntity> to the IList<Entity>.
         */
        if (field.ValueType == typeof(Entity))
        {
            /* Create new list
             * As property value is a IList<IRevitEntity>, I can get
             * size of the list and pass it to the new list
             */
            IList<Entity> entityList = new List<Entity>(propertyValue.Count);

            foreach (IRevitEntity revitEntity in propertyValue)
            {
                //convert each IRevitEntity to the ExStorage.Entity
                Entity convertedEntity = Convert(revitEntity);
                entityList.Add(convertedEntity);
            }

            return entityList;
        }

        /* If generic type is a primitive types,
         * just return source property value as IList<>
         */
        return propertyValue;
    }

    /// <summary>
    /// Convert ExStorage.Entity to the IRevitEntity object
    /// </summary>
    /// <typeparam name="TRevitEntity">The type of the IRevitEntity</typeparam>
    /// <param name="entity">Entity to convert</param>
    /// <returns>Converted IRevitEntity</returns>
    public TRevitEntity Convert<TRevitEntity>(Entity entity) where TRevitEntity : class, IRevitEntity
    {
        Type revitEntityType = typeof(TRevitEntity);

        /* Create new instance of the TRevitEntity which is a class
         * that implements IRevitEntity interface
        */
        TRevitEntity revitEntity = Activator.CreateInstance<TRevitEntity>();

        Schema schema = entity.Schema;

        Type entityType = typeof(Entity);

        /* Iterate all of the schema fields
         * and set the IRevit entity property, with
         * FieldName name, with value of fieldValue
         */
        IList<Field> schemaFields = schema.ListFields();
        foreach (Field field in schemaFields)
        {
            // Get the property of the IRevitEntity class
            PropertyInfo property = revitEntityType.GetProperty(field.FieldName);

            /*
             * Get the field value of the entity
             * I.e. call Entity.Get<FieldTypeValue>
             * As we don't know the FieldTypeValue at
             * the compile time, we invoke the
             * Entity.Get<> method via reflection
             */
            object entityValue = null;
            switch (field.ContainerType)
            {
                case ContainerType.Simple:

                    entityValue = GetEntityFieldValue(property, entity, field, field.ValueType);

                    if (entityValue == null)
                    {
                        continue;
                    }

                    if (entityValue is Entity)
                    {
                        if (((Entity)entityValue).Schema != null)
                        {
                            entityValue = Convert(property.PropertyType, entityValue as Entity);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    break;
                case ContainerType.Array:

                    /*
                     * Call Entity.Get<FieldType>(Field field) method
                    */
                    Type iListType = typeof(IList<>);
                    Type genericIlistType = iListType.MakeGenericType(field.ValueType);

                    /* Get the field value from entity.
                     * As Field.Container type is an Array,
                     * the entity value has IList<T> type.
                     */
                    entityValue = GetEntityFieldValue(property, entity, field, genericIlistType);

                    if (entityValue == null)
                    {
                        continue;
                    }

                    IList listEntityValues = entityValue as IList;

                    /* create a new instance of a property of the IRevitEntity
                     * object which implements IList<T> interface.
                     */
                    IList listProperty;

                    /* property type which implements IList<T> interface,
                     * may have constructor with capacity parameter.
                     * If have, pass as capacity. If not - create
                     * instance with default constructor
                     */

                    if (property.PropertyType.GetConstructor(new Type[] { typeof(int) }) != null)
                    {
                        listProperty = Activator.CreateInstance(property.PropertyType, new object[] { listEntityValues.Count }) as IList;
                    }
                    else
                    {
                        listProperty = Activator.CreateInstance(property.PropertyType) as IList;
                    }


                    /* if field.ValueType is Entity
                     * We must convert all of the Entity
                     * to the IRevitEntity
                     * I.e. get IList<IRevitEntity> from
                     * IList<Entity>
                     */
                    if (field.ValueType == typeof(Entity))
                    {

                        /* Get the generic type of the IList
                         * it should be IRevitEntity.
                         * So, we convert an Entity to an IRevitEntity
                         */
                        Type iRevitEntityType = property.PropertyType.GetGenericArguments()[0];

                        foreach (Entity listEntityValue in listEntityValues)
                        {
                            object convertedEntity = Convert(iRevitEntityType, listEntityValue);
                            listProperty.Add(convertedEntity);
                        }
                    }
                    else
                    {
                        foreach (object value in listEntityValues)
                        {
                            listProperty.Add(value);
                        }
                    }

                    entityValue = listProperty;

                    break;

                // IDictionary<,>
                case ContainerType.Map:
                    /*
                    * Call Entity.Get<FieldType>(Field field) method
                   */
                    Type iDicitonaryType = typeof(IDictionary<,>);
                    Type genericIDicitionaryType = iDicitonaryType.MakeGenericType(field.KeyType, field.ValueType);

                    /* Get the field value from entity.
                     * As Field.Container type is an Array,
                     * the entity value has IList<T> type.
                     */
                    entityValue = GetEntityFieldValue(property, entity, field, genericIDicitionaryType);

                    if (entityValue == null)
                    {
                        continue;
                    }

                    IDictionary mapEntityValues = entityValue as IDictionary;

                    /* create a new instance of a property of the IRevitEntity
                    * object which implements IList<T> interface.
                    */
                    IDictionary dictProperty;

                    if (property.PropertyType.GetConstructor(new[] { typeof(int) }) != null)
                    {
                        dictProperty = Activator.CreateInstance(property.PropertyType, new object[] { mapEntityValues.Count }) as IDictionary;
                    }
                    else
                    {
                        dictProperty = Activator.CreateInstance(property.PropertyType) as IDictionary;
                    }

                    /* if field.ValueType is Entity
                     * We must convert all of the Entity
                     * to the IRevitEntity
                     * I.e. get IDictionary<T, IRevitEntity> from
                     * IDictionary<T, Entity>
                     */
                    if (field.ValueType == typeof(Entity))
                    {

                        /* Get the generic type of the IList
                         * it should be IRevitEntity.
                         * So, we convert an Entity to an IRevitEntity
                         */
                        Type iRevitEntityType = property.PropertyType.GetGenericArguments()[1];

                        foreach (dynamic keyValuePair in mapEntityValues)
                        {
                            dynamic convertedEntity = Convert(iRevitEntityType, keyValuePair.Value);

                            dictProperty.Add(keyValuePair.Key, convertedEntity);
                        }
                    }
                    else
                    {
                        foreach (dynamic keyValuePair in mapEntityValues)
                        {
                            dictProperty.Add(keyValuePair.Key, keyValuePair.Value);
                        }
                    }

                    entityValue = dictProperty;

                    break;
            }

            if (entityValue != null)
                property.SetValue(revitEntity, entityValue, null);
        }

        return revitEntity;
    }

    #endregion

    private object GetEntityFieldValue(PropertyInfo property, Entity entity, Field field, Type fieldValueType)
    {
        /*
         * When we save entity to an element and
         * entity has an SubEntity we should ommit
         * set Subentity. And there is a cse would happen
         * when there is no subschema loaded into the memory.
         * In this case, Revit throws exception about
         * "There is no Schema with id in memory"
         */
        if (field.SubSchemaGUID != Guid.Empty && field.SubSchema == null)
        {
            return null;
        }

        AttributeExtractor<FieldAttribute> fieldAttributeExtractor = new();
        FieldAttribute fieldAttribute = fieldAttributeExtractor.GetAttribute(property);

        object entityValue;

        if (fieldAttribute.UnitTypeId == null)
        {
            MethodInfo entityGetMethod = entity
                .GetType()
                .GetMethod("Get", new[] { typeof(Field) });

            MethodInfo entityGetMethodGeneric = entityGetMethod.MakeGenericMethod(fieldValueType);

            entityValue = entityGetMethodGeneric.Invoke(entity, new[] { field });
        }
        else
        {
            ForgeTypeId id = new(fieldAttribute.UnitTypeId);
            MethodInfo entityGetMethod = entity
                .GetType()
                .GetMethod("Get", new[] { typeof(Field), typeof(ForgeTypeId) });

            MethodInfo entityGetMethodGeneric = entityGetMethod.MakeGenericMethod(fieldValueType);

            entityValue = entityGetMethodGeneric.Invoke(entity, new object[] { field, id });
        }

        return entityValue;
    }

    private object Convert(Type irevitEntityType, Entity entity)
    {
        MethodInfo convertMethod = GetType().GetMethod("Convert", new[] { typeof(Entity) });
        MethodInfo convertMethodGeneric = convertMethod.MakeGenericMethod(irevitEntityType);

        object iRevitEntity = convertMethodGeneric.Invoke(this, new[] { entity });

        return iRevitEntity;
    }
}
