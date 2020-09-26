using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Test project")]
[assembly: SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500:Braces for multi-line statements should not share line", Justification = "Source package. Will fix later.", Scope = "member", Target = "~M:NServiceBus.AcceptanceTests.Recoverability.When_cross_q_transactional_message_is_moved_to_error_queue.Should_not_dispatch_outgoing_messages~System.Threading.Tasks.Task")]