namespace BiblePresenter.App.Services;

public sealed class BibleImportException : Exception
{
    public BibleImportException(string message) : base(message) { }
    public BibleImportException(string message, Exception inner) : base(message, inner) { }
}
