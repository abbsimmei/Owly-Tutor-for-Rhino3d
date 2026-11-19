namespace Rhino.AI;

internal enum AttachmentKind
{
    Image,
    TextFile,
}

// Payload-free because it hangs off a turn in the transcript, where Data would keep every image the session sent in memory.
internal sealed record AttachmentInfo(AttachmentKind Kind, string Name, string MediaType, int Bytes);

// Dumb data object; Data is the raw payload (Part 4 consumes it).
internal sealed record Attachment(AttachmentKind Kind, string Name, string MediaType, byte[] Data)
{
    public AttachmentInfo Info => new(Kind, Name, MediaType, Data.Length);
}
