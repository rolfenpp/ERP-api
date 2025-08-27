public static class Permissions
{
    // Dashboard
    public const string ViewDashboard = "view_dashboard";

    // Inventory
    public const string ViewInventory = "view_inventory";
    public const string EditInventory = "edit_inventory";
    public const string DeleteInventory = "delete_inventory";
    public const string CreateInventory = "create_inventory";

    // Invoices
    public const string ViewInvoices = "view_invoices";
    public const string EditInvoices = "edit_invoices";
    public const string DeleteInvoices = "delete_invoices";
    public const string CreateInvoices = "create_invoices";

    // Projects (matches your existing controller)
    public const string ViewProjects = "view_projects";
    public const string EditProjects = "edit_projects";
    public const string DeleteProjects = "delete_projects";
    public const string CreateProjects = "create_projects";

    // User Management
    public const string ManageUsers = "manage_users";
    public const string AssignPermissions = "assign_permissions";

    public static readonly string[] All = new[]
    {
        ViewDashboard,
        ViewInventory, EditInventory, DeleteInventory, CreateInventory,
        ViewInvoices, EditInvoices, DeleteInvoices, CreateInvoices,
        ViewProjects, EditProjects, DeleteProjects, CreateProjects,
        ManageUsers, AssignPermissions
    };
}