using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MESI
{
    class Simulate
    {
        #region Global Variables
        public string[] ProcessorFile = { "1.PRG", "2.PRG", "3.PRG", "4.PRG", "5.PRG", "6.PRG", "7.PRG", "8.PRG" };

        int numOfProcessor;
        int cacheSize;
        int blockSize;
        string BenchMarkFile;

        List<Processor> processors;
        Bus bus;

        //How the memory address can be split into.
        string offsetAddress;
        string slotAddress;
        string tagAddress;
        #endregion

        public Simulate(string[] args)
        {
            BenchMarkFile = args[0];
            numOfProcessor = Convert.ToInt32(args[1]);
            cacheSize = Convert.ToInt32(args[2]);
            blockSize = Convert.ToInt32(args[3]);

            processors = new List<Processor>(numOfProcessor);
            bus = new Bus();
        }

        public void runSimulation()
        {
            #region Initialization
            List<StreamReader> trace = new List<StreamReader>(numOfProcessor);
            //Initiliaze the processors and the traces
            for (int i = 0; i < numOfProcessor; i++)
            {
                string tempfileName = Constants.filePath + BenchMarkFile + ProcessorFile[i];
                //string tempfileName = @"C:\Users\Saurabh\Documents\Lab\Yr4 Sem1\CS4223\Assignment 3\Sample.txt";
                Processor p = new Processor(cacheSize, blockSize, tempfileName, i);
                processors.Add(p);
                trace.Add(new StreamReader(processors[i].fileName));
            }
            #endregion

            while (true) //This loop will keep on running till the break statment is encountered.
            {//Entry here is at a new cycle.
                foreach (var processor in processors)
                {

                    //Counter will be incremented irrespective of the instruction type (fetch, read or write) & even if it is blocked
                    processor.incrementCounter();

                    if (bus.inUse) //If the bus is already in use, increment the cycles for which it has been in use.
                    {
                        bus.useCycles++;
                        if (bus.useCycles == Constants.memoryToCache_cycles) //Bus has been blocked for 10 cycles
                        {
                            //serve the processor who had blocked the bus (this is for simulation coz in this implementaion, when a processor had a Cache Miss it got blocked and the cache would return result after 10 clock cycles. Hence I am updating the state after 10 clock cycles since the processor got blocked.)
                            updateBlockedState();
                        }
                    }

                    try //in try catch block coz one trace might get empty before some other processors trace.
                    {
                        //Check if the current processor is blocked or not.
                        if (!processor.isBlocked)
                        {
                            string line = trace[processor.processorId].ReadLine();
                            runOnTrace(processor.processorId, line);
                        }
                        else
                            continue; //Do nothing for this processor as it is blocked.
                    }
                    catch (Exception)
                    {
                    }
                }
                int empty = 0;
                foreach (var item in trace)
                {
                    if (item.EndOfStream == true)
                        empty++;
                }
                if (empty == numOfProcessor)
                    break; //The while loop has to break when all the trace files have been executed.
            }

            #region Print Miss Ratio
            //int totalCachemiss = 0;
            //int totalMem = 0;
            foreach (var processor in processors)
            {
                int cacheMiss = processor.memoryAccess - processor.cacheHit;
                Console.WriteLine("Cache Miss ratio for processor{1} is: {0}", (double)cacheMiss / processor.memoryAccess, processor.processorId + 1);
                Console.WriteLine();
                //totalCachemiss += cacheMiss;
                //totalMem += processor.memoryAccess;
            }
            //Console.WriteLine("Average Cache Miss Ratio is: {0}", (double)totalCachemiss / totalMem);
            #endregion
        }

        private void runOnTrace(int processorId, string line)
        {
            Label label;
            CacheAccessResult result = CacheAccessResult.ReadHit; //This will change later. Simply assigned to avoid compilation error.

            label = corresondingLabel(line[0]);

            //Only need to simulate Data Cache
            if (label != Label.Fetch)
            {
                //This means it is a memory access
                processors[processorId].incrementMemoryAccess();

                //Split the address to get the tag and slot
                splitAddress(line.Substring(2));

                if (existsInCache(processorId) == true) // Cache Hit
                {
                    processors[processorId].incrementCacheHit();

                    if (label == Label.Read)
                        result = CacheAccessResult.ReadHit;
                    if (label == Label.Write)
                        result = CacheAccessResult.WriteHit;
                }
                else //Cache Miss
                {
                    if (label == Label.Read)
                        result = CacheAccessResult.ReadMiss;
                    if (label == Label.Write)
                        result = CacheAccessResult.WriteMiss;
                }

                runCacheProtocol(result, processorId);
            }
        }

        private void runCacheProtocol(CacheAccessResult currentResult, int processorId)
        {
            int slot = convertBinarytoDecimal(slotAddress);

            switch (currentResult)
            {
                case CacheAccessResult.ReadHit: //No blocking. do nothing. There will be no state change and no bus use.
                    bus.pendingSignal.Enqueue(BusSignals.NoSignal);
                    break;

                case CacheAccessResult.ReadMiss:
                    bus.pendingSignal.Enqueue(BusSignals.BusRd);
                    //block the processor
                    bus.putBus_toUse(processorId, currentResult, processors[processorId].L1.blocks[slot].currentState);

                    //currentProcessor will block
                    processors[processorId].isBlocked = true;
                    break;

                case CacheAccessResult.WriteHit: //No blocking. //Can be because of being in MES state.
                    bus.pendingSignal.Enqueue(BusSignals.BusInvalidate);
                    break;

                case CacheAccessResult.WriteMiss:
                    bus.pendingSignal.Enqueue(BusSignals.BusRdX);
                    //block the processor
                    bus.putBus_toUse(processorId, currentResult, processors[processorId].L1.blocks[slot].currentState);

                    //currentProcessor will block
                    processors[processorId].isBlocked = true;
                    break;

                default:
                    break;
            }

            snoopBus(processorId);
        }

        private void updateBlockedState()
        {
            //Update the state of the blocked cache and other caches depending upon ReadMiss or WriteMiss
            //Deque the top processor and reset the BlockCycles.
            //If queue is empty, remove the bus from use.
            int slot = convertBinarytoDecimal(slotAddress);
            blockingInfo blocked = bus.waitingProcessors.Dequeue();

            //Unblock the processor which was just served.
            processors[blocked.processorId].isBlocked = false;

            if (bus.waitingProcessors.Count != 0) //There are other processors waiting in line
                bus.inUse = true; //bus again in use.

            else //No more processors waiting in line.
                bus.inUse = false;

            bus.useCycles = 0; //reset the use cycles irrespective of the bus being in used or not.
        }

        private bool existsInCache(int processorId)
        {
            int slot = convertBinarytoDecimal(slotAddress);
            if (processors[processorId].L1.blocks[slot].tag == tagAddress && processors[processorId].L1.blocks[slot].currentState != MESI_States.Invalid)
                return true;
            else
                return false;
        }

        private void snoopBus(int sendingProcessorId)
        {
            int slot = convertBinarytoDecimal(slotAddress);
            bool sharedSignal = false;
            int processorSupplyingData = -1;
            BusSignals servingSignal = bus.pendingSignal.Peek();

            #region otherProcessorsSnooping
            foreach (var processor in processors) //each processor snoops the bus
            {
                if (processor.L1.blocks[slot].tag != tagAddress || processor.processorId == sendingProcessorId) //The processor does not have that data and do not process the signal on the sending processor.
                    continue;
                else
                {
                    MESI_States ownState = processor.L1.blocks[slot].currentState;
                    switch (servingSignal)
                    {
                        case BusSignals.BusRd:
                            switch (ownState)
	                            {
                                    case MESI_States.Modified:
                                    //flush data to memory
                                        flushData();
                                        processor.L1.blocks[slot].currentState = MESI_States.Shared;
                                        sharedSignal = true;
                                        processorSupplyingData = processor.processorId;
                                        break;
                                    case MESI_States.Exclusive:
                                        processor.L1.blocks[slot].currentState = MESI_States.Shared;
                                        sharedSignal = true;
                                        processorSupplyingData = processor.processorId;
                                        break;
                                case MESI_States.Shared:
                                    //do nothing. let it be at shared
                                        sharedSignal = true;
                                        processorSupplyingData = processor.processorId;
                                        break;
                                    case MESI_States.Invalid:
                                    //do nothing.
                                        break;
                                    default:
                                        break;
	                            }
                            break;

                        case BusSignals.BusRdX:
                            switch (ownState)
                            {
                                case MESI_States.Modified:
                                    flushData();
                                    processor.L1.blocks[slot].currentState = MESI_States.Invalid;
                                    processorSupplyingData = processor.processorId;
                                    break;
                                case MESI_States.Exclusive:
                                    processor.L1.blocks[slot].currentState = MESI_States.Invalid;
                                    processorSupplyingData = processor.processorId;
                                    break;
                                case MESI_States.Shared:
                                    processor.L1.blocks[slot].currentState = MESI_States.Invalid;
                                    processorSupplyingData = processor.processorId;
                                    break;
                                case MESI_States.Invalid:
                                    //do nothing.
                                    break;
                                default:
                                    break;
                            }
                            break;

                        case BusSignals.BusInvalidate:
                            switch (ownState)
                            {
                                case MESI_States.Modified:
                                    //not possible. Only the sending processor can have modified data.
                                    break;
                                case MESI_States.Exclusive:
                                    processor.L1.blocks[slot].currentState = MESI_States.Invalid;
                                    break;
                                case MESI_States.Shared:
                                    processor.L1.blocks[slot].currentState = MESI_States.Invalid;
                                    break;
                                case MESI_States.Invalid:
                                    //do nothing.
                                    break;
                                default:
                                    break;
                            }
                            break;
                        case BusSignals.NoSignal:
                            //do nothing.
                            break;
                        default:
                            break;
                    }
                }
            }
            #endregion

            #region respondingToResponse
            //update the sending processor's state.
            switch (servingSignal)
            {
                case BusSignals.BusRd:
                    //the processor did not have the data. It will go into Exclusive or Shared state depending on if the shared signal was raised.
                    if (sharedSignal == true)
                    {
                        processors[sendingProcessorId].L1.blocks[slot].currentState = MESI_States.Shared;
                        //store data
                        processors[sendingProcessorId].L1.blocks[slot].tag = processors[processorSupplyingData].L1.blocks[slot].tag;
                    }
                    else
                    {
                        processors[sendingProcessorId].L1.blocks[slot].currentState = MESI_States.Exclusive;
                        //store data
                        processors[sendingProcessorId].L1.blocks[slot].tag = tagAddress;
                    }
                    break;

                case BusSignals.BusRdX:
                    //It will have modified (dirty) data coz it is asking for data with intent to modify
                    processors[sendingProcessorId].L1.blocks[slot].currentState = MESI_States.Modified;

                    if (processorSupplyingData != -1) //if some processor had this data
                        //store data
                        processors[sendingProcessorId].L1.blocks[slot].tag = processors[processorSupplyingData].L1.blocks[slot].tag;
                    else
                    {
                        //store data
                        processors[sendingProcessorId].L1.blocks[slot].tag = tagAddress;
                    }
                    break;
                case BusSignals.BusInvalidate:
                    //It is about to modify its own copy.
                    processors[sendingProcessorId].L1.blocks[slot].currentState = MESI_States.Modified;
                    break;
                case BusSignals.NoSignal:
                    break;
                default:
                    break;
            }
            #endregion

            bus.pendingSignal.Dequeue(); //Signal has been served.
        }

        #region Working Helper functions
        private double find2PowerX(int num)
        {
            return (Math.Log10(num) / Math.Log10(2));
        }

        //Convert the hexadecimal address to binary so that we are able to obtain the tag bits and slot bits from the given address
        private string convertHextoBinary(string hexAddress)
        {
            string binaryval = "";
            binaryval = Convert.ToString(Convert.ToInt32(hexAddress, 16),2);

            //now make sure that this address is 32-bits
            while (binaryval.Length != 32)
            {
                binaryval = "0" + binaryval;
            }
            return binaryval;
        }

        //This function will be used to convert the binary form of the Slot number obatined from the memory address
        //into an integer to locate the slot number in the cache block. A cache is an array of cacheBlock and the index of the 
        //cache block (index is the slot number) will take us to that slot.
        //e.g. cacheBlock[slot number in integer] = the slot where this memory address will map to in the cache.
        private int convertBinarytoDecimal(string binaryAddress)
        {
            long l = Convert.ToInt64(binaryAddress, 2);
            int i = (int)l;
            return i;
        }

        private void splitAddress(string hexAddress)
        {
            int offsetBits = Convert.ToInt32(find2PowerX(blockSize));
            int slotBits = Convert.ToInt32(find2PowerX(cacheSize / blockSize)); //No. of blocks = cacheSize/blockSize;
            int tagBits = Constants.addressLenght - (offsetBits + slotBits);

            //Console.WriteLine("Offset : {0}", offsetBits);
            //Console.WriteLine("Slot : {0}", slotBits);
            //Console.WriteLine("Tag : {0}", tagBits);

            string binaryAddress = convertHextoBinary(hexAddress);
            //Console.WriteLine("Binary Address : {0}", binaryAddress);

            tagAddress = binaryAddress.Substring(0, tagBits);
            slotAddress = binaryAddress.Substring(tagBits, slotBits);
            offsetAddress = binaryAddress.Substring(tagBits + slotBits);

            //Console.WriteLine("Offset : {0}", offset);
            //Console.WriteLine("Slot : {0}", slot);
            //Console.WriteLine("Slot in Decimal : {0}", convertBinarytoDecimal(slot));
            //Console.WriteLine("Tag : {0}", tag);
        }

        private Label corresondingLabel(char labelNumber)
        {
            switch (labelNumber)
            {
                case '0':
                    return Label.Fetch;
                case '2':
                    return Label.Read;
                case '3':
                    return Label.Write;
                default:
                    return Label.Fetch;
            }
        }

        //Checks if all the trace files have reached their end.
        private bool EndofTrace(List<StreamReader> t)
        {
            foreach (var item in t)
            {
                if (item.EndOfStream == false)
                {
                    return false;
                }
            }
            return true;
        }

        private void flushData()
        {
            //Function which would write back 
        }

        #endregion
    }
}
