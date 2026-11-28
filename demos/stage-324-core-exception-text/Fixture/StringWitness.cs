public static class StringWitness
{
    public static int Length(string value) => value.Length;
    public static int Character(string value, int index) => value[index];
}
