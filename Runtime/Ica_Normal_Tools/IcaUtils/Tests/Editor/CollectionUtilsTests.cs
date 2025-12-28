using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Ica.Utils.Tests
{
    public class CollectionUtilsTests
    {
        [Test]
        public void UnrollList_EmptyLists()
        {
            var nested = new UnsafeList<NativeList<int>>(1, Allocator.Temp);
            nested.Add(new NativeList<int>(Allocator.Temp));
            nested.Add(new NativeList<int>(Allocator.Temp));
            nested.Add(new NativeList<int>(Allocator.Temp));

            var unrolled = new NativeList<int>(1, Allocator.Temp);
            var unrolledMapper = new NativeList<int>(1, Allocator.Temp);
            Ica.Utils.NativeContainerUtils.UnrollListsToList(nested,ref unrolled,ref unrolledMapper);
            
            Assert.AreEqual(unrolled.Length,0);

        }
        



        [Test]
        public void Get_NestedTotalSize_List()
        {
            var nested = new UnsafeList<NativeList<int>>(1, Allocator.Temp);
            nested.Add(new NativeList<int>(Allocator.Temp) { 0, 0, 0 });
            nested.Add(new NativeList<int>(Allocator.Temp) { 1, 1 });
            nested.Add(new NativeList<int>(Allocator.Temp) { 2 });

            Ica.Utils.NativeContainerUtils.GetTotalSizeOfNestedContainer(nested, out var size);
            Assert.IsTrue(size == 6);
        }

        [Test]
        public void Get_NestedTotalSize_Array()
        {
            var nested = new UnsafeList<NativeArray<int>>(1, Allocator.Temp);
            nested.Add(new NativeArray<int>(3, Allocator.Temp));
            nested.Add(new NativeArray<int>(2, Allocator.Temp));
            nested.Add(new NativeArray<int>(1, Allocator.Temp));

            Ica.Utils.NativeContainerUtils.GetTotalSizeOfNestedContainer(nested, out var size);
            Assert.IsTrue(size == 6);
        }
    }
}