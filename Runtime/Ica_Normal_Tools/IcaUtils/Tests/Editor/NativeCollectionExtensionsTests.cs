using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine.TestTools;

namespace Ica.Utils.Tests
{
    public class NativeCollectionExtensionsTests
    {
        [Test]
        public void InsertAtBeginningNativeList_InsertInt_()
        {
            var toInsert = 98765;
            var listOriginal = new NativeList<int>(1, Allocator.Temp) { 0, 1, 2, 3, 4 };
            var listModified = new NativeList<int>(1, Allocator.Temp) { 0, 1, 2, 3, 4 };

            listModified.InsertAtBeginning(toInsert);

            Assert.AreEqual(listModified[0], toInsert);
            for (int i = 0; i < listOriginal.Length; i++)
            {
                Assert.AreEqual(listOriginal[i], listModified[i + 1]);
            }
        }

        [Test]
        public void InsertAtBeginningNativeList_EmptyList_()
        {
            var list = new NativeList<int>(Allocator.Temp);
            list.InsertAtBeginning(3);
            list.InsertAtBeginning(2);
            list.InsertAtBeginning(1);
            list.InsertAtBeginning(0);

            Assert.AreEqual(list.Length, 4);
            for (int i = 0; i < list.Length; i++)
            {
                Assert.AreEqual(list[i], i);
            }
        }

        [Test]
        public void InsertNativeList_InsertIntToMiddle_()
        {
            var list = new NativeList<int>(Allocator.Temp) { 0, 1, 2, 4, 5 };

            var toCompare = new NativeList<int>(Allocator.Temp) { 0, 1, 2, 3, 4, 5 };

            list.Insert(3, 3);

            Assert.AreEqual(list.AsArray().ToArray(), toCompare.AsArray().ToArray());
        }

        [Test]
        public void InsertNativeList_InsertIntToEnd_()
        {
            var list = new NativeList<int>(Allocator.Temp) { 0  };

            var toCompare = new NativeList<int>(Allocator.Temp) { 0, 1, 2, 3, 4, 5 };

            list.Insert(list.Length, 1);
            list.Insert(list.Length, 2);
            list.Insert(list.Length, 3);
            list.Insert(list.Length, 4);
            list.Insert(list.Length, 5);


            Assert.AreEqual(list.AsArray().ToArray(), toCompare.AsArray().ToArray());
        }
        [Test]
        public void InsertNativeList_InsertIntToEndEptyList_()
        {
            var list = new NativeList<int>(Allocator.Temp) {  };

            var toCompare = new NativeList<int>(Allocator.Temp) { 0, 1, 2, 3, 4, 5 };

            list.Insert(list.Length, 0);
            list.Insert(list.Length, 1);
            list.Insert(list.Length, 2);
            list.Insert(list.Length, 3);
            list.Insert(list.Length, 4);
            list.Insert(list.Length, 5);


            Assert.AreEqual(list.AsArray().ToArray(), toCompare.AsArray().ToArray());
        }
    }
}