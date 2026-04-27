namespace CC.Domain.Notifications;

public static class NotificationEventCodes
{
  public const string PasswordReset = "PASSWORD_RESET";
  public const string TenantAdminActivation = "TENANT_ADMIN_ACTIVATION";
  public const string TenantActivated = "TENANT_ACTIVATED";
  public const string TenantSuspended = "TENANT_SUSPENDED";
  public const string TenantReactivated = "TENANT_REACTIVATED";
  public const string TenantDisabled = "TENANT_DISABLED";
  public const string UserInvitation = "USER_INVITATION";
  public const string OrderCreated = "ORDER_CREATED";
  public const string OrderShipped = "ORDER_SHIPPED";
  public const string OrderDelivered = "ORDER_DELIVERED";
  public const string OrderCancelled = "ORDER_CANCELLED";
  public const string PaymentApproved = "PAYMENT_APPROVED";
  public const string PaymentRejected = "PAYMENT_REJECTED";
}