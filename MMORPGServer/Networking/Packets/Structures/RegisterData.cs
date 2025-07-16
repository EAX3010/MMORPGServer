namespace MMORPGServer.Networking.Packets.Structures
{
    public record RegisterData(
      string Name,
      short Body,
      short Class,
      uint CreatedAtFingerPrint,
      string CreatedAtMacAddress);

}
