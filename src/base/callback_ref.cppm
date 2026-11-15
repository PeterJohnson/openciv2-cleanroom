export module openciv.base.callback_ref;

import std;

export namespace openciv::base {
/// Non-owning callable view. The bound callable must outlive every invocation;
/// bindings must not be replaced while a dependent operation is active. Only
/// lvalues bind, so a temporary closure cannot silently become retained state.
/// Copying this view preserves callable identity and never copies its state.
template <class Signature> class CallbackRef;
/// A borrowed callable with the signature R(Args...); copying preserves its identity.
template <class R, class... Args> class CallbackRef<R(Args...)> {
public:
    CallbackRef() = default;
    /// Create an empty callback view.
    CallbackRef(std::nullptr_t) {}
    /// Bind a function pointer without taking ownership.
    CallbackRef(R (*function)(Args...)) noexcept : function_(function) {}
    /// Borrow a callable lvalue; its lifetime must cover every invocation.
    template <class F>
        requires(!std::same_as<std::remove_cv_t<F>, CallbackRef> && std::is_object_v<F> &&
                 std::is_invocable_r_v<R, F&, Args...>)
    CallbackRef(F& callable) {
        if constexpr (requires { static_cast<bool>(callable); })
            if (!static_cast<bool>(callable))
                return;
        target_ = std::addressof(callable);
        invoke_ = [](const void* target, Args... args) -> R {
            auto& bound = *const_cast<F*>(static_cast<const F*>(target));
            if constexpr (std::is_void_v<R>)
                std::invoke(bound, std::forward<Args>(args)...);
            else
                return std::invoke(bound, std::forward<Args>(args)...);
        };
    }
    /// Report whether a callable is bound.
    explicit operator bool() const noexcept { return invoke_ != nullptr || function_ != nullptr; }
    /// Invoke the borrowed callable with forwarded arguments.
    /// @throws std::bad_function_call If no callable is bound.
    R operator()(Args... args) const {
        if (function_)
            return function_(std::forward<Args>(args)...);
        if (!invoke_)
            throw std::bad_function_call();
        return invoke_(target_, std::forward<Args>(args)...);
    }

private:
    R (*function_)(Args...) = nullptr;
    const void* target_ = nullptr;
    R (*invoke_)(const void*, Args...) = nullptr;
};
} // namespace openciv::base
