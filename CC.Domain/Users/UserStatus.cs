namespace CC.Domain.Users;

public enum UserStatus
{
  PendingActivation = 0,
  Active = 1,
  Inactive = 2,
  Suspended = 3,
  Locked = 4,
  Deleted = 5
}