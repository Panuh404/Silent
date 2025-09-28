namespace Silent.Core.Text
{
    public interface IBuffer
    {
        int Length { get; }
        string GetText(int start, int length);
        void Insert(int position, string text);
        void Delete(int start, int length);

        event EventHandler? Changed;
    }
}
