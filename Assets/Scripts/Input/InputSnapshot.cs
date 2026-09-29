namespace M4U.Input
{
    public readonly record struct CommonInputSnapshot(bool Cancel);

    public readonly record struct PlayerInputSnapshot(float Horizontal, float Vertical, bool Jump);
}
