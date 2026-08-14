namespace Bookshop.Lending.Checkout.for_Checkout.given;

public class a_registered_member : Specification
{
    protected Member _member;
    protected Checkout _checkout;

    void Establish()
    {
        _member = Members.Ursula;
        _checkout = new Checkout(_catalog, _loans, _clock);
    }
}
