using Cratis.Specifications;
using Xunit;

namespace Bookshop.Lending.Checkout.for_Checkout;

public class when_borrowing_a_book_with_no_available_copy : given.a_registered_member
{
    Exception _result;

    void Because() => _result = Catch.Exception(() => _checkout.Borrow(Book.TheDispossessed, _member));

    [Fact] void should_explain_that_no_copy_is_available() => _result.Message.ShouldEqual("There are no copies available");
    [Fact] void should_not_create_a_loan() => _loans.ShouldBeEmpty();
}
