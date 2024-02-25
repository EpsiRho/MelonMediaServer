// Function to update user profile
function updateUserProfile() {
    document.getElementById('user-picture').src = userProfile.profilePicture;
    document.getElementById('username').textContent = userProfile.username;
    document.getElementById('bio').textContent = userProfile.bio;
}

updateUserProfile();