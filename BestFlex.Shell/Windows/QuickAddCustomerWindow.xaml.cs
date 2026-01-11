using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using BestFlex.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BestFlex.Shell.Windows
{
    public partial class QuickAddCustomerWindow : Window
    {
        public object? CreatedCustomer { get; private set; }

        public QuickAddCustomerWindow()
        {
            InitializeComponent();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = (txtName.Text ?? "").Trim();
            var phone = (txtPhone.Text ?? "").Trim();
            var email = (txtEmail.Text ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show(this, "Name is required.", "Add Customer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }

            try
            {
                var sp = ((App)System.Windows.Application.Current).Services;
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BestFlexDbContext>();

                // Resolve the DbSet<CustomerAccounts> *property* (no generic Set<T>() calls).
                var dbSetProp = typeof(BestFlexDbContext)
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p =>
                        p.PropertyType.IsGenericType &&
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                        string.Equals(p.Name, "CustomerAccounts", StringComparison.OrdinalIgnoreCase));

                if (dbSetProp == null)
                    throw new InvalidOperationException("CustomerAccounts DbSet was not found in BestFlexDbContext.");

                var entityType = dbSetProp.PropertyType.GetGenericArguments()[0];
                var setObj = dbSetProp.GetValue(db) ?? throw new InvalidOperationException("Failed to get DbSet instance.");

                // IQueryable<object> so EF can translate EF.Property("Name")
                var qObj = ((IQueryable)setObj).Cast<object>();
                bool exists = await EntityFrameworkQueryableExtensions.AnyAsync(
                    qObj,
                    e2 => EF.Property<string>(e2, "Name") == name
                );

                if (exists)
                {
                    MessageBox.Show(this, "A customer with this name already exists.",
                        "Add Customer", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create entity instance and set properties reflectively
                var entity = Activator.CreateInstance(entityType)!;

                SetRequired(entityType, entity, "Name", name);
                SetOptional(entityType, entity, "Phone", phone);
                SetOptional(entityType, entity, "Mobile", phone);
                SetOptional(entityType, entity, "Email", email);
                SetOptional(entityType, entity, "CreatedAt", DateTimeOffset.Now);

                // Add and save
                var addMethod = setObj.GetType().GetMethod("Add", new[] { entityType });
                if (addMethod == null) // fallback: parameter type-less resolution works, too
                    addMethod = setObj.GetType().GetMethod("Add");
                addMethod!.Invoke(setObj, new[] { entity });

                await db.SaveChangesAsync();

                CreatedCustomer = entity;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to save customer.\n\n{ex.Message}", "Add Customer",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void SetRequired(Type t, object instance, string propName, object value)
        {
            var p = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite)
                throw new InvalidOperationException($"Customer entity is missing writable '{propName}'.");
            if (value is string s && string.IsNullOrWhiteSpace(s))
                throw new InvalidOperationException($"'{propName}' is required.");
            p.SetValue(instance, ConvertTo(p.PropertyType, value));
        }

        private static void SetOptional(Type t, object instance, string propName, object? value)
        {
            var p = t.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite) return;
            if (value is string s && string.IsNullOrWhiteSpace(s)) return;
            p.SetValue(instance, ConvertTo(p.PropertyType, value));
        }

        private static object? ConvertTo(Type targetType, object? value)
        {
            if (value == null) return null;
            if (targetType.IsInstanceOfType(value)) return value;

            if (targetType == typeof(DateTime) && value is DateTimeOffset dto) return dto.LocalDateTime;
            if (targetType == typeof(DateTimeOffset) && value is DateTime dt) return new DateTimeOffset(dt);
            if (targetType == typeof(string)) return value.ToString();

            return value; // best effort
        }
    }
}
