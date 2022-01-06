// Copyright 2021 Eldar Nizamutdinov deim.mobile<at>gmail.com 
//  
// Licensed under the Apache License, Version 2.0 (the "License")
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at 
//
//     http://www.apache.org/licenses/LICENSE-2.0 
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections;
using System.Reflection;
using TSParser.Tables;

namespace TSParser.Comparer
{
    internal class Compare
    {
        private List<string> m_difference = new List<string>();
        public List<string> AreEqual(Table t1, Table t2)
        {
            if (t1 == null || t2 == null)
            {
                m_difference.Add("Try to compare null object");
                return m_difference;
            }

            Type t1Type = t1.GetType();

            if (t1Type != t2.GetType())
            {
                m_difference.Add("Can't compare different types");
                return m_difference;
            }

            if (t1.CRC32 != 0 && t2.CRC32 != 0)
            {
                if (t1.CRC32 == t2.CRC32)
                {
                    m_difference.Add($"{t1Type.Name} equals");
                    return m_difference;
                }
            }



            return m_difference;
        }

        private void Check(object objectA, object objectB, string callCollection = null)
        {
            if (objectA == null && objectB == null)
            {
                m_difference.Add($"NUll -> NULL");
                return;
            }

            if (objectA == null && objectB != null)
            {
                if (callCollection != null)
                {
                    m_difference.Add($"{callCollection}: None -> {objectB.GetType().Name}");
                }
                else
                {
                    m_difference.Add($"None -> {objectB.GetType().Name}");
                }
                return;
            }

            if (objectA != null && objectB == null)
            {
                if (callCollection != null)
                {
                    m_difference.Add($"{callCollection}: {objectA.GetType().Name} -> None");
                }
                else
                {
                    m_difference.Add($"{objectA.GetType().Name} -> None");
                }
                return;
            }

            Type objectType = objectA!.GetType();

            if (objectType != objectB!.GetType())
            {
                if (callCollection != null)
                {
                    m_difference.Add($"{callCollection}: Type changed: {objectType} -> {objectB.GetType()}");
                }
                else
                {
                    m_difference.Add($"Type changed: {objectType} -> {objectB.GetType()}");
                }

                return;
            }

            foreach (PropertyInfo propertyInfo in objectType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead))
            {

                object valueA = propertyInfo.GetValue(objectA, null)!;
                object valueB = propertyInfo.GetValue(objectB, null)!;
                //drop byte[]
                if ((valueA != null && valueA.GetType().Name == "Byte[]") || (valueB != null && valueB.GetType().Name == "Byte[]"))
                {
                    continue;
                }

                //Debug.WriteLine($"{propertyInfo.Name}");

                if (CanDirectlyCompare(propertyInfo.PropertyType))
                {
                    if (AreValueEqual(valueA, valueB))
                    {

                    }
                    else
                    {
                        if (callCollection != null)
                        {
                            m_difference.Add($"{callCollection}: {propertyInfo.Name}: {valueA} -> {valueB}");
                        }
                        else
                        {
                            m_difference.Add($"{propertyInfo.Name}: {valueA} -> {valueB}");
                        }

                    }


                }
                else if (typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
                {

                    IEnumerable<object> collectionItems1;
                    IEnumerable<object> collectionItems2;
                    int collectionItemsCount1;
                    int collectionItemsCount2;

                    if (valueA == null && valueB != null)
                    {
                        collectionItems2 = ((IEnumerable)valueB).Cast<object>();
                        collectionItemsCount2 = collectionItems2.Count();

                        for (int i = 0; i < collectionItemsCount2; i++)
                        {
                            if (callCollection != null)
                            {
                                m_difference.Add($"{callCollection}: Position {i}: None -> {collectionItems2.ElementAt(i)}");
                            }
                            else
                            {
                                m_difference.Add($"Position {i}: None -> {collectionItems2.ElementAt(i)}");
                            }

                        }

                        continue;
                    }

                    if (valueA != null && valueB == null)
                    {
                        collectionItems1 = ((IEnumerable)valueA).Cast<object>();
                        collectionItemsCount1 = collectionItems1.Count();

                        for (int i = 0; i < collectionItemsCount1; i++)
                        {
                            if (callCollection != null)
                            {
                                m_difference.Add($"{callCollection}: Position {i}: {collectionItems1.ElementAt(i)} -> None");
                            }
                            else
                            {
                                m_difference.Add($"Position {i}: {collectionItems1.ElementAt(i)} -> None");
                            }

                        }

                        continue;
                    }

                    if (valueA != null && valueB != null)
                    {
                        collectionItems1 = ((IEnumerable)valueA).Cast<object>();
                        collectionItems2 = ((IEnumerable)valueB).Cast<object>();
                        collectionItemsCount1 = collectionItems1.Count();
                        collectionItemsCount2 = collectionItems2.Count();

                        if (collectionItemsCount1 != collectionItemsCount2)
                        {
                            if (callCollection != null)
                            {
                                m_difference.Add($"{callCollection}: Different in {propertyInfo.Name} count: {collectionItemsCount1} -> {collectionItemsCount2}");
                            }
                            else
                            {
                                m_difference.Add($"Different in {propertyInfo.Name} count: {collectionItemsCount1} -> {collectionItemsCount2}");
                            }

                        }

                        var maxCount = collectionItemsCount1 > collectionItemsCount2 ? collectionItemsCount1 : collectionItemsCount2;

                        for (int i = 0; i < maxCount; i++)
                        {
                            object collectionItem1 = (i > collectionItemsCount1 - 1 ? null : collectionItems1.ElementAt(i))!;
                            object collectionItem2 = (i > collectionItemsCount2 - 1 ? null : collectionItems2.ElementAt(i))!;
                            Type collectionItemType = (collectionItem1?.GetType() ?? collectionItem2?.GetType())!;

                            if (CanDirectlyCompare(collectionItemType))
                            {
                                if (!AreValueEqual(valueA, valueB))
                                {
                                    if (callCollection != null)
                                    {
                                        m_difference.Add($"{callCollection}: {propertyInfo.Name}, {collectionItemType.Name}: {valueA} -> {valueB}");
                                    }
                                    else
                                    {
                                        m_difference.Add($"{propertyInfo.Name}, {collectionItemType.Name}: {valueA} -> {valueB}");
                                    }

                                }
                            }
                            else
                            {
                                Check(collectionItem1!, collectionItem2!, $"{propertyInfo.Name}, position: {i}");
                            }
                        }
                    }
                }
                else if (propertyInfo.PropertyType.IsClass)
                {
                    m_difference.Add($"Property is class: {propertyInfo.Name}");
                    Check(propertyInfo.GetValue(objectA, null)!, propertyInfo.GetValue(objectB, null)!);
                }
                else
                {
                    m_difference.Add($"Cannot compare property: {propertyInfo.Name}, {propertyInfo.PropertyType}");
                }

            }


        }

        private bool CanDirectlyCompare(Type type)
        {
            //Debug.WriteLine($"{type.Name}, {typeof(IComparable).IsAssignableFrom(type)}, {type.IsPrimitive}, {type.IsValueType}");
            return typeof(IComparable).IsAssignableFrom(type) || type.IsPrimitive;// || type.IsValueType;
        }

        private bool AreValueEqual(object valueA, object valueB)
        {

            bool result;
            IComparable selfValueComparer;

            selfValueComparer = (valueA as IComparable)!;

            if (selfValueComparer != null && selfValueComparer.CompareTo(valueB) != 0)
                result = false; // the comparison using IComparable failed
            else if (!object.Equals(valueA, valueB))
                result = false; // the comparison using Equals failed
            else
                result = true; // match

            return result;
        }
    }
}
