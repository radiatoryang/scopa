using Unity.Collections;

namespace Ica.Utils
{
    // This is the example code used in
// Packages/com.unity.collections/Documentation~/allocator/allocator-rewindable.md
// Example user structure
    public struct RewindableStruct
    {
        // Use AllocatorHelper to help creating a rewindable alloctor
        private AllocatorHelper<RewindableAllocator> _rwdAllocatorHelper;

        // Rewindable allocator property for accessibility
        public ref RewindableAllocator RwdAllocator => ref _rwdAllocatorHelper.Allocator;

        // Create the rewindable allocator
        void CreateRewindableAllocator(AllocatorManager.AllocatorHandle backgroundAllocator, int initialBlockSize, bool enableBlockFree = false)
        {
            // Allocate the rewindable allocator from backgroundAllocator and register the allocator
            _rwdAllocatorHelper = new AllocatorHelper<RewindableAllocator>(backgroundAllocator);

            // Allocate the first memory block with initialBlockSize in bytes, and indicate whether
            // to enable the rewindable allocator with individual block free through enableBlockFree
            RwdAllocator.Initialize(initialBlockSize, enableBlockFree);
        }

        // Constructor of user structure
        public RewindableStruct(int initialBlockSize)
        {
            this = default;
            CreateRewindableAllocator(Allocator.Persistent, initialBlockSize, false);
        }

        // Dispose the user structure
        public void Dispose()
        {
            DisposeRewindableAllocator();
        }

        // // Sample code to use rewindable allocator to allocate containers
        // public void UseRewindableAllocator(out NativeArray<int> nativeArray, out NativeList<int> nativeList)
        // {
        //     // Use rewindable allocator to allocate a native array, no need to dispose the array manually
        //     // CollectionHelper is required to create/allocate native array from a custom allocator.
        //     nativeArray = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(100, ref RwdAllocator);
        //     nativeArray[0] = 0xFE;
        //
        //     // Use rewindable allocator to allocate a native list, do not need to dispose the list manually
        //     nativeList = new NativeList<int>(RwdAllocator.Handle);
        //     for (int i = 0; i < 50; i++)
        //     {
        //         nativeList.Add(i);
        //     }
        //
        //     // // Use custom allocator to allocate a byte buffer.
        //     // bytePtr = (byte*)AllocatorManager.Allocate(ref RwdAllocator, sizeof(byte), sizeof(byte), 10);
        //     // bytePtr[0] = 0xAB;
        // }

        // Free all allocations from the rewindable allocator
        public void FreeRewindableAllocator()
        {
            RwdAllocator.Rewind();
        }

        // Dispose the rewindable allocator
        void DisposeRewindableAllocator()
        {
            // Dispose all the memory blocks in the rewindable allocator
            RwdAllocator.Dispose();
            // Unregister the rewindable allocator and dispose it
            _rwdAllocatorHelper.Dispose();
        }
    }
}