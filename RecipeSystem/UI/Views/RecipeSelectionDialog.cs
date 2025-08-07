using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TeachingPendant.RecipeSystem.Models;
using TeachingPendant.UserManagement.Services;
using TeachingPendant.Logging;

namespace TeachingPendant.RecipeSystem.UI.Views
{
    /// <summary>
    /// 레시피 선택 다이얼로그
    /// RecipeRunner에서 실행할 레시피를 선택하기 위한 간단한 다이얼로그
    /// </summary>
    public class RecipeSelectionDialog : Window
    {
        #region Private Fields
        private ListBox _recipeListBox;
        private Button _selectButton;
        private Button _cancelButton;
        private TextBox _searchTextBox;
        private ObservableCollection<RecipeListItem> _recipeItems;
        private CollectionViewSource _recipeViewSource;
        private const string SOURCE = "RecipeSelectionDialog"; // 로그 소스 상수
        #endregion

        #region Public Properties
        /// <summary>
        /// 선택된 레시피
        /// </summary>
        public TransferRecipe SelectedRecipe { get; private set; }
        #endregion

        #region Constructor
        /// <summary>
        /// 레시피 선택 다이얼로그 생성자
        /// </summary>
        public RecipeSelectionDialog()
        {
            InitializeDialog();
            LoadAvailableRecipes();
            Logger.Info(SOURCE, "Constructor", "Recipe selection dialog has been created.");
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 다이얼로그 초기화
        /// </summary>
        private void InitializeDialog()
        {
            try
            {
                Title = "Select Recipe";
                Width = 600;
                Height = 400;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.CanResize;

                CreateDialogContent();
                SetupEventHandlers();
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "InitializeDialog", "Error occurred while initializing recipe selection dialog", ex);
            }
        }

        /// <summary>
        /// 다이얼로그 UI 생성
        /// </summary>
        private void CreateDialogContent()
        {
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 검색 영역
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 목록 영역
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 버튼 영역

            // 검색 영역
            var searchPanel = CreateSearchPanel();
            Grid.SetRow(searchPanel, 0);
            mainGrid.Children.Add(searchPanel);

            // 레시피 목록 영역
            var listPanel = CreateListPanel();
            Grid.SetRow(listPanel, 1);
            mainGrid.Children.Add(listPanel);

            // 버튼 영역
            var buttonPanel = CreateButtonPanel();
            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;
        }

        /// <summary>
        /// 검색 패널 생성
        /// </summary>
        private Panel CreateSearchPanel()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10),
                Height = 35
            };
            var searchLabel = new Label
            {
                Content = "Search:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness { Left = 0, Top = 0, Right = 5, Bottom = 0 }
            };
            _searchTextBox = new TextBox
            {
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness { Left = 0, Top = 0, Right = 10, Bottom = 0 }
            };
            var refreshButton = new Button
            {
                Content = "Refresh",
                Padding = new Thickness(10, 5, 10, 5),
                VerticalAlignment = VerticalAlignment.Center
            };
            refreshButton.Click += RefreshButton_Click;
            panel.Children.Add(searchLabel);
            panel.Children.Add(_searchTextBox);
            panel.Children.Add(refreshButton);
            return panel;
        }
        /// <summary>
        /// 목록 패널 생성
        /// </summary>
        private Border CreateListPanel()
        {
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(10, 0, 10, 0)
            };
            _recipeListBox = new ListBox
            {
                DisplayMemberPath = "DisplayText",
                SelectionMode = SelectionMode.Single
            };
            _recipeItems = new ObservableCollection<RecipeListItem>();
            _recipeViewSource = new CollectionViewSource { Source = _recipeItems };
            _recipeListBox.ItemsSource = _recipeViewSource.View;
            border.Child = _recipeListBox;
            return border;
        }
        /// <summary>
        /// 버튼 패널 생성
        /// </summary>
        private Panel CreateButtonPanel()
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            _selectButton = new Button
            {
                Content = "Select",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 0, 5, 0),
                IsDefault = true,
                IsEnabled = false
            };
            _cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 0, 5, 0),
                IsCancel = true
            };
            panel.Children.Add(_selectButton);
            panel.Children.Add(_cancelButton);
            return panel;
        }

        /// <summary>
        /// 이벤트 핸들러 설정
        /// </summary>
        private void SetupEventHandlers()
        {
            _selectButton.Click += SelectButton_Click;
            _cancelButton.Click += CancelButton_Click;
            _recipeListBox.SelectionChanged += RecipeListBox_SelectionChanged;
            _searchTextBox.TextChanged += SearchTextBox_TextChanged;
            _recipeListBox.MouseDoubleClick += RecipeListBox_MouseDoubleClick;
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 선택 버튼 클릭 이벤트
        /// </summary>
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_recipeListBox.SelectedItem is RecipeListItem selectedItem)
                {
                    SelectedRecipe = LoadSelectedRecipe(selectedItem.RecipeName);
                    if (SelectedRecipe != null)
                    {
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        MessageBox.Show("Could not load the selected recipe.", "Error",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "SelectButton_Click", "Error occurred while selecting recipe", ex);
                MessageBox.Show("An error occurred while selecting the recipe: " + ex.Message, "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 취소 버튼 클릭 이벤트
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 레시피 목록 선택 변경 이벤트
        /// </summary>
        private void RecipeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectButton.IsEnabled = _recipeListBox.SelectedItem != null;
        }

        /// <summary>
        /// 레시피 목록 더블클릭 이벤트
        /// </summary>
        private void RecipeListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_recipeListBox.SelectedItem != null)
            {
                SelectButton_Click(sender, null);
            }
        }

        /// <summary>
        /// 검색 텍스트 변경 이벤트
        /// </summary>
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_recipeViewSource != null && _recipeViewSource.View != null)
                {
                    string searchText = "";
                    if (_searchTextBox.Text != null)
                    {
                        searchText = _searchTextBox.Text.Trim().ToLower();
                    }

                    if (string.IsNullOrEmpty(searchText))
                    {
                        _recipeViewSource.View.Filter = null;
                    }
                    else
                    {
                        _recipeViewSource.View.Filter = obj =>
                        {
                            if (obj is RecipeListItem item)
                            {
                                return item.RecipeName.ToLower().Contains(searchText) ||
                                       item.Description.ToLower().Contains(searchText);
                            }
                            return false;
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "SearchTextBox_TextChanged", "Error occurred while applying search filter", ex);
            }
        }

        /// <summary>
        /// 새로고침 버튼 클릭 이벤트
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadAvailableRecipes();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// 사용 가능한 레시피 목록 로드 (C# 6.0 호환 버전)
        /// </summary>
        private void LoadAvailableRecipes()
        {
            try
            {
                _recipeItems.Clear();

                if (!PermissionChecker.HasPermission("RECIPE_VIEW"))
                {
                    MessageBox.Show("You do not have permission to view recipes.", "Permission Denied",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var random = new Random();
                var dummyRecipes = new List<string>
                {
                    "Default_Wafer_Transfer_Recipe", "HighSpeed_Transfer_Recipe", "Precision_Placement_Recipe", "Test_Recipe"
                };

                foreach (var recipeName in dummyRecipes)
                {
                    try
                    {
                        var listItem = new RecipeListItem
                        {
                            RecipeName = recipeName,
                            Description = recipeName + " description",
                            StepCount = 6,
                            CreatedDate = DateTime.Now.AddDays(-random.Next(30)),
                            DisplayText = recipeName + " (6 steps) - " + recipeName + " description"
                        };
                        _recipeItems.Add(listItem);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(SOURCE, "LoadAvailableRecipes", "Failed to load recipe info: " + recipeName, ex);
                    }
                }

                Logger.Info(SOURCE, "LoadAvailableRecipes", "Recipe list loaded successfully: " + _recipeItems.Count.ToString() + " items");
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "LoadAvailableRecipes", "Error occurred while loading recipe list", ex);
                MessageBox.Show("An error occurred while loading the recipe list: " + ex.Message, "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 선택된 레시피 로드
        /// </summary>
        private TransferRecipe LoadSelectedRecipe(string recipeName)
        {
            try
            {
                return CreateDummyRecipe(recipeName);
            }
            catch (Exception ex)
            {
                Logger.Error(SOURCE, "LoadSelectedRecipe", "Error occurred while loading recipe: " + recipeName, ex);
                return null;
            }
        }

        /// <summary>
        /// 더미 레시피 생성 (테스트용)
        /// </summary>
        private TransferRecipe CreateDummyRecipe(string recipeName)
        {
            var recipe = new TransferRecipe
            {
                RecipeName = recipeName,
                Description = recipeName + " Description",
                CreatedBy = "TestUser",
                CreatedDate = DateTime.Now
            };

            recipe.Steps.Add(new RecipeStep { Type = StepType.Home, Description = "Move to Home position", TeachingGroupName = "TestGroup", TeachingLocationName = "Home" });
            recipe.Steps.Add(new RecipeStep { Type = StepType.Move, Description = "Move to pickup position", TeachingGroupName = "TestGroup", TeachingLocationName = "PickPosition" });
            recipe.Steps.Add(new RecipeStep { Type = StepType.Pick, Description = "Pickup wafer", TeachingGroupName = "TestGroup", TeachingLocationName = "PickPosition" });

            return recipe;
        }
        #endregion
    }

    /// <summary>
    /// 레시피 목록 표시를 위한 클래스 (클래스 외부로 이동)
    /// </summary>
    public class RecipeListItem
    {
        public string RecipeName { get; set; }
        public string Description { get; set; }
        public int StepCount { get; set; }
        public DateTime CreatedDate { get; set; }
        public string DisplayText { get; set; }
    }
}