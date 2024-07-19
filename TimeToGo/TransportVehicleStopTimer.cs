using Colossal.Serialization.Entities;
using Unity.Burst;
using Unity.Entities;

namespace TimeToGo
{
    public struct TransportVehicleStopTimer : IComponentData, ISerializable
    {
        public static readonly SharedStatic<uint> Interval = SharedStatic<uint>.GetOrCreate<TransportVehicleStopTimer>();

        public uint StartFrame { get; set; }

        public bool ShouldStop(uint now)
        {
            if (StartFrame == 0) return false;
            if (StartFrame > now)
            {
                return true;
            }

            if (now - StartFrame > Interval.Data)
            {
                return true;
            }

            return false;
        }

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(StartFrame);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out uint t);
            StartFrame = t;
        }
    }
}