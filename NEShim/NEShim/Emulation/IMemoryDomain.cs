namespace NEShim.Emulation;

internal interface IMemoryDomain
{
    byte PeekByte(long address);
}
