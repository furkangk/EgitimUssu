using EgitimUssu.Modules.Messaging.Application;
using EgitimUssu.Shared.Application;
using EgitimUssu.Shared.Infrastructure;
using EgitimUssu.Shared.Kernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EgitimUssu.Shared.Contracts;

namespace EgitimUssu.Modules.Messaging.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagingModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<MessagingDbContext>(configuration, "Messaging", MessagingDbContext.SchemaName);

        services.AddScoped<IMessagingRepository, MessagingRepository>();
        services.AddScoped<IConversationRelationshipPolicy, ConversationRelationshipPolicy>();

        services.AddScoped<
            ICommandHandler<OpenConversationCommand, Result<ConversationResponse>>,
            OpenConversationCommandHandler>();
        services.AddScoped<
            ICommandHandler<SendMessageCommand, Result<MessageResponse>>,
            SendMessageCommandHandler>();
        services.AddScoped<
            ICommandHandler<MarkConversationReadCommand, Result>,
            MarkConversationReadCommandHandler>();
        services.AddScoped<
            ICommandHandler<SetConversationBlockCommand, Result<ConversationResponse>>,
            SetConversationBlockCommandHandler>();
        services.AddScoped<
            ICommandHandler<DeleteMessageCommand, Result>,
            DeleteMessageCommandHandler>();

        services.AddScoped<
            IQueryHandler<ListConversationsQuery, Result<IReadOnlyList<ConversationSummaryResponse>>>,
            ListConversationsQueryHandler>();
        services.AddScoped<
            IQueryHandler<ListMessagesQuery, Result<IReadOnlyList<MessageResponse>>>,
            ListMessagesQueryHandler>();

        // Tek örnek bütün mesajlaşma uçlarının kapısını taşır: hepsinin koşulu aynı — oturum açık olmalı.
        services.AddScoped<AuthenticatedMessagingAuthorizer>();
        services.AddScoped<ICommandAuthorizer<OpenConversationCommand>>(
            provider => provider.GetRequiredService<AuthenticatedMessagingAuthorizer>());
        services.AddScoped<ICommandAuthorizer<SendMessageCommand>>(
            provider => provider.GetRequiredService<AuthenticatedMessagingAuthorizer>());
        services.AddScoped<ICommandAuthorizer<MarkConversationReadCommand>>(
            provider => provider.GetRequiredService<AuthenticatedMessagingAuthorizer>());
        services.AddScoped<ICommandAuthorizer<SetConversationBlockCommand>>(
            provider => provider.GetRequiredService<AuthenticatedMessagingAuthorizer>());
        services.AddScoped<ICommandAuthorizer<DeleteMessageCommand>>(
            provider => provider.GetRequiredService<AuthenticatedMessagingAuthorizer>());
        services.AddScoped<IQueryAuthorizer<ListConversationsQuery>>(
            provider => provider.GetRequiredService<AuthenticatedMessagingAuthorizer>());
        services.AddScoped<IQueryAuthorizer<ListMessagesQuery>>(
            provider => provider.GetRequiredService<AuthenticatedMessagingAuthorizer>());
        // E-07: KVKK hesap silme — bu modüldeki kişisel veriyi anonimleştirir/siler.
        services.AddScoped<IPersonalDataAnonymizer, MessagingPersonalDataAnonymizer>();


        return services;
    }
}
