# Project: High-Performance Inline Text Comparison Tool

## Objective
Create a C# Windows Forms desktop application that compares two bodies of text, completely ignores line breaks, performs a word-level diff, and displays an inline result (showing original deleted words struck-through next to new inserted words highlighted). The application must be performant enough to handle large texts without freezing the UI thread.

## Tech Stack & Prerequisites
* **Framework:** .NET 8.0 (or .NET 6.0+) Windows Forms Application.
* **Language:** C# (latest).
* **External Dependencies:** The `DiffPlex` NuGet package (for highly optimized, Myers-based diffing of large sequences).

## UI Specifications
The main form (`Form1`) should use a responsive layout (e.g., `TableLayoutPanel` or `SplitContainer`) so it scales when maximized.
It must contain the following controls:
1.  `txtOriginal` (TextBox): Multiline = True, ScrollBars = Vertical.
2.  `txtModified` (TextBox): Multiline = True, ScrollBars = Vertical.
3.  `rtbResult` (RichTextBox): ReadOnly = True, ScrollBars = Vertical.
4.  `btnCompare` (Button): Text = "Compare Texts".
5.  `lblStatus` (Label): To show "Processing..." or "Done" to the user.

## Step-by-Step Implementation Guide

### Step 1: Preprocessing
Create a method to sanitize the input strings.
* Read `txtOriginal.Text` and `txtModified.Text`.
* Strip out all line breaks and carriage returns (`\r` and `\n`) completely.

### Step 2: Tokenization (Word Splitting)
Create a method that takes the sanitized strings and splits them into arrays of tokens (words, spaces, and punctuation).
* **Crucial Regex:** Use `Regex.Split(text, @"(\s+|[.,!?;:])")`. 
* Filter out any empty or null strings from the resulting array. 
* *Note:* It is vital to retain spaces and punctuation as independent tokens in the array so the final output doesn't lose its formatting.

### Step 3: Diff Calculation (Off the UI Thread)
Create a background-safe method to calculate the differences using `DiffPlex`.
* Initialize the `Differ` class from the `DiffPlex` library.
* Use `Differ.CreateCustomDiffs(tokens1, tokens2, false)` or manually map the DiffPlex algorithm to compare the two `string[]` arrays.
* Map the DiffPlex output blocks to a sequential list of operations (Equal, Delete, Insert) combined with the corresponding token text.
* **Requirement:** This entire calculation must be invoked using `await Task.Run(...)` inside the `btnCompare_Click` event so the UI does not freeze during large document comparisons.

### Step 4: UI Rendering (The Output)
Create a method to render the sequential diff operations into the `rtbResult` control.
* **Performance Requirement:** You MUST call `rtbResult.SuspendLayout()` before modifying the text, and `rtbResult.ResumeLayout()` when finished, to prevent UI locking and flickering.
* Clear the `rtbResult` box.
* Iterate through the diff operations and append text by manipulating the `SelectionStart`, `SelectionLength`, `SelectionFont`, `SelectionColor`, and `SelectionBackColor` properties.
* **Formatting Rules:**
    * *Equal:* Normal font, Color: `Color.Black`, Background: Default.
    * *Delete:* Strikethrough font (`FontStyle.Strikeout`), Color: `Color.Gray`.
    * *Insert:* Bold font (`FontStyle.Bold`), Color: `Color.DarkRed`, Background: `Color.FromArgb(255, 230, 230)` (light pink).

### Step 5: Event Wiring & State Management
* Disable `btnCompare` and set `lblStatus.Text` to "Processing..." right before the `Task.Run` starts.
* Re-enable the button and update the status when the task and rendering are complete.
* Wrap the execution in a `try/catch` block and display any exceptions in a `MessageBox`.