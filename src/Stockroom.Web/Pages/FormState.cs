using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Stockroom.Web.Pages;

public static class FormState
{
    // Returns "true" for a field with errors so assistive technology announces it as invalid;
    // null omits the aria-invalid attribute.
    public static string? Invalid(ModelStateDictionary state, string key) =>
        state.GetFieldValidationState(key) == ModelValidationState.Invalid ? "true" : null;
}
