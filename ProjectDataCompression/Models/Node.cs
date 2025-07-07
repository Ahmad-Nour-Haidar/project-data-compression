namespace ProjectDataCompression.Project;

public class Node
{
    public byte Symbol;
    public int Frequency;
    public Node? Left, Right;
    public bool IsLeaf => Left == null && Right == null;
}