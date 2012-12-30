using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MESI
{
    public struct blockingInfo
    {
        public int processorId; public CacheAccessResult result; public MESI_States processorState;
    }

       public enum Label   //The label from the PRG File
	{
            Fetch, Read, Write
	}

       public enum MESI_States
       {
           Modified, Exclusive, Shared, Invalid
       }

       public enum BusSignals
       {
           BusRd, BusRdX, BusInvalidate, NoSignal
       }

       public enum CacheAccessResult
       {
           ReadHit, ReadMiss, WriteHit, WriteMiss
       }

    static class Constants
    {
        public const int PARAMS = 4;
        public const int addressLenght = 32;  //32 bit memory address
        public const int memoryToCache_cycles = 10;
        public const string filePath = @"C:\Users\Saurabh\Documents\Lab\Yr4 Sem1\CS4223\Assignment 3\Weather8\";
    }
}
