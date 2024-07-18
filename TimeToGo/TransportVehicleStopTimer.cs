using Colossal.Serialization.Entities;

using Unity.Entities;

namespace TimeToGo
{
    public struct TransportVehicleStopTimer : IComponentData, ISerializable
    {
        public uint StartFrame { get; set; }

        public bool ShouldStop(uint now)
        {
            TimeToGo.Logger.Info($"now is:{now} start is:{StartFrame}");
            if (StartFrame == 0) return false;
            if (now - StartFrame > 1)
            {
                TimeToGo.Logger.Info("Force stop");
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