namespace ProjectDataCompression;

using System.Windows.Forms;

public static class PasswordDialog
{
    public static string RequestPassword(string prompt = "Enter Password")
    {
        // Create a new form
        Form promptForm = new Form()
        {
            Width = 300,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = prompt,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false
        };

        // Label
        Label textLabel = new Label() { Left = 20, Top = 20, Text = prompt, AutoSize = true };

        // Password TextBox
        TextBox passwordBox = new TextBox() { Left = 20, Top = 50, Width = 240, PlaceholderText = "Password" };
        passwordBox.UseSystemPasswordChar = true;

        // OK button
        Button confirmation = new Button()
            { Text = "OK", Left = 100, Width = 80, Top = 80, DialogResult = DialogResult.OK };
        confirmation.Click += (sender, e) => { promptForm.Close(); };

        promptForm.Controls.Add(textLabel);
        promptForm.Controls.Add(passwordBox);
        promptForm.Controls.Add(confirmation);
        promptForm.AcceptButton = confirmation;

        // Show dialog and return password if OK clicked
        return promptForm.ShowDialog() == DialogResult.OK ? passwordBox.Text : "";
    }
}