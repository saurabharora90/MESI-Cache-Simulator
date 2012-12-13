using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MESI
{
    class CacheBlock
    {
        public string tag { get; set; }
        public MESI_States currentState { get; set; }
        public int slot { get; set; } //Block number

        public CacheBlock(int blockNumber)
        {
            //At initilaization, cache is empty hence state is Invalid and tag is empty;
            currentState = MESI_States.Invalid;
            slot = blockNumber;
            tag = null;
        }
    }

    class Cache
    {
        public List<CacheBlock> blocks;
        public int numOfBlock { get; set; }

        public Cache(int cs, int bs)
        {
            numOfBlock = cs / bs;
            blocks = new List<CacheBlock>(numOfBlock);

            for (int i = 0; i < numOfBlock; i++)
            {
                CacheBlock b = new CacheBlock(i); //i is the block number of each block
                blocks.Add(b);
            }
        }
    }

    class Processor
    {
        public int cycleCounter { get; set; }
        public Cache L1;
        public int cacheSize { get; set; }
        public int blockSize { get; set; }
        public int processorId { get; set; }
        public bool isBlocked { get; set; }

        public int memoryAccess { get; set; }
        public int cacheHit { get; set; }

        //Each processor will store the location of the trace file it is supposed to run.
        public string fileName { get; set; }

        public Processor(int cs, int bs, string traceFile, int pId)
        {
            L1 = new Cache(cs, bs);
            cycleCounter = 0;
            cacheSize = cs;
            blockSize = bs;
            fileName = traceFile;
            processorId = pId;
            isBlocked = false;
        }

        public void incrementCounter()
        {
            cycleCounter++;
        }

        public void incrementMemoryAccess()
        {
            memoryAccess++;
        }

        public void incrementCacheHit()
        {
            cacheHit++;
        }
    }

    class Bus
    {
        public int useCycles { get; set; }

        public Queue<blockingInfo> waitingProcessors;
        public bool inUse { get; set; }
        public Queue<BusSignals> pendingSignal;

        public Bus()
        {
            useCycles = 0;
            waitingProcessors = new Queue<blockingInfo>();
            inUse = false;
            pendingSignal = new Queue<BusSignals>();
        }

        public void resetBlockCycles()
        {
            this.useCycles = 0;
        }

        public void putBus_toUse(int pID, CacheAccessResult result, MESI_States pS, bool used = false)
        {
            if (this.waitingProcessors.Count == 0) //First entry into the queue else the bus would already be in use.
            {
                this.useCycles = 0;
                this.inUse = true;
            }

            blockingInfo temp;
            temp.processorId = pID;
            temp.processorState = pS;
            temp.result = result;
            this.waitingProcessors.Enqueue(temp);
        }
    }
}
