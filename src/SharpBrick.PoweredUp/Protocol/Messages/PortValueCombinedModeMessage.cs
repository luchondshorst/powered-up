namespace SharpBrick.PoweredUp.Protocol.Messages
{
    public class PortValueCombinedModeMessage : PoweredUpMessage
    {
        public byte PortId { get; set; }

        public PortValueData[] Data { get; set; }

        public override string ToString()
            => $"Port Values (Combined Mode) - " + PortValueSingleMessage.FormatPortValueDataArray(HubId, Data);
    }
}