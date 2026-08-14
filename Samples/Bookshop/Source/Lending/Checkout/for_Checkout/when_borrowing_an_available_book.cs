using Cratis.Specifications;
using Xunit;

namespace Bookshop.Lending.Checkout.for_Checkout;

public class when_borrowing_an_available_book : given.a_registered_member
{
    CheckoutReceipt _receipt;

    void Establish() => _catalog.Add(AvailableBook.TheLeftHandOfDarkness);

    void Because() => _receipt = _checkout.Borrow(AvailableBook.TheLeftHandOfDarkness, _member);

    [Fact] void should_confirm_the_loan() => _receipt.Confirmed.ShouldBeTrue();
    [Fact] void should_set_the_due_date_three_weeks_ahead() => _receipt.DueDate.ShouldEqual(_today.AddDays(21));
    [Fact] void should_make_the_copy_unavailable() => _catalog.AvailableCopies.ShouldEqual(0);
}
