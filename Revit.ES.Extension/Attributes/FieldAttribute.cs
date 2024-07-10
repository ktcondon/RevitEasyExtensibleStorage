/* 
 * THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
 * PARTICULAR PURPOSE.
 * 
 */

namespace Revit.ES.Extension.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class FieldAttribute : Attribute
{
    public FieldAttribute()
    {
        UnitTypeId = null;
        SpecTypeId = null;
    }

    public string Documentation { get; set; }
    public string UnitTypeId { get; set; }
    public string SpecTypeId { get; set; }
}
