(function () {
  'use strict';

  var selected = { type: null, id: null };

  // Tab switching
  var tabBtns = document.querySelectorAll('.bd-tab-btn');
  tabBtns.forEach(function (btn) {
    btn.addEventListener('click', function () {
      tabBtns.forEach(function (b) { b.classList.remove('is-active'); });
      btn.classList.add('is-active');
      document.querySelectorAll('.bd-tab-content').forEach(function (s) {
        s.classList.remove('active');
      });
      document.getElementById('bd-tab-' + btn.dataset.tab).classList.add('active');
      selected.type = null;
      selected.id   = null;
    });
  });
  if (tabBtns.length) tabBtns[0].click();

  // ApiClient helpers
  function serverUrl() {
    return window.ApiClient ? window.ApiClient.serverAddress() : '';
  }

  function userId() {
    return window.ApiClient.getCurrentUserId();
  }

  function apiGet(path) {
    if (!window.ApiClient) throw new Error('ApiClient not ready');
    return window.ApiClient.getJSON(path);
  }

  function token() {
    return window.ApiClient.accessToken();
  }

  // Playlists
  async function loadPlaylists() {
    var statusEl = document.getElementById('bd-status-playlists');
    var listEl   = document.getElementById('bd-list-playlists');
    try {
      var data = await apiGet('/Users/' + userId() + '/Items?IncludeItemTypes=Playlist&Recursive=true&Fields=Id,Name');
      statusEl.textContent = '';
      if (!data.Items || data.Items.length === 0) { statusEl.textContent = 'No playlists found.'; return; }
      listEl.innerHTML = '';
      data.Items.forEach(function (item) {
        var li = document.createElement('li');
        li.innerHTML = '<label><input type="radio" name="bd-sel" value="' + item.Id + '" /> ' + escHtml(item.Name) + '</label>';
        li.querySelector('input').addEventListener('change', function () { selected.type = 'playlist'; selected.id = item.Id; });
        listEl.appendChild(li);
      });
    } catch (e) {
      statusEl.textContent = 'Failed to load playlists.';
    }
  }

  // TV Shows
  async function loadTvShows() {
    var statusEl = document.getElementById('bd-status-tvshows');
    var listEl   = document.getElementById('bd-list-tvshows');
    try {
      var data = await apiGet('/Users/' + userId() + '/Items?IncludeItemTypes=Series&Recursive=true&Fields=Id,Name');
      statusEl.textContent = '';
      if (!data.Items || data.Items.length === 0) { statusEl.textContent = 'No TV series found.'; return; }
      listEl.innerHTML = '';
      data.Items.forEach(function (series) {
        var li = document.createElement('li');
        li.innerHTML =
          '<label><input type="radio" name="bd-sel" value="' + series.Id + '" class="bd-series-radio" /> ' + escHtml(series.Name) + '</label>' +
          '<button class="emby-button bd-expand-btn" data-sid="' + series.Id + '">&#9660; Seasons</button>' +
          '<ul class="bd-sub-list" id="bd-seasons-' + series.Id + '" style="display:none"></ul>';
        li.querySelector('.bd-series-radio').addEventListener('change', function () { selected.type = 'series'; selected.id = series.Id; });
        li.querySelector('.bd-expand-btn').addEventListener('click', async function () {
          var seasonList = document.getElementById('bd-seasons-' + series.Id);
          if (seasonList.innerHTML === '') await loadSeasons(series.Id, seasonList);
          seasonList.style.display = seasonList.style.display === 'none' ? 'block' : 'none';
        });
        listEl.appendChild(li);
      });
    } catch (e) {
      statusEl.textContent = 'Failed to load TV series.';
    }
  }

  async function loadSeasons(seriesId, containerEl) {
    try {
      var data = await apiGet('/Shows/' + seriesId + '/Seasons?userId=' + userId() + '&Fields=Id,Name');
      if (!data.Items) return;
      data.Items.forEach(function (season) {
        var li = document.createElement('li');
        li.innerHTML = '<label><input type="radio" name="bd-sel" value="' + season.Id + '" /> ' + escHtml(season.Name) + '</label>';
        li.querySelector('input').addEventListener('change', function () { selected.type = 'season'; selected.id = season.Id; });
        containerEl.appendChild(li);
      });
    } catch (e) {
      containerEl.innerHTML = '<li>Failed to load seasons.</li>';
    }
  }

  // Audiobooks
  async function loadAudiobooks() {
    var statusEl = document.getElementById('bd-status-audiobooks');
    var listEl   = document.getElementById('bd-list-audiobooks');
    try {
      var data = await apiGet('/Users/' + userId() + '/Items?IncludeItemTypes=MusicAlbum&Recursive=true&Fields=Id,Name');
      statusEl.textContent = '';
      if (!data.Items || data.Items.length === 0) { statusEl.textContent = 'No albums / audiobooks found.'; return; }
      listEl.innerHTML = '';
      data.Items.forEach(function (item) {
        var li = document.createElement('li');
        li.innerHTML = '<label><input type="radio" name="bd-sel" value="' + item.Id + '" /> ' + escHtml(item.Name) + '</label>';
        li.querySelector('input').addEventListener('change', function () { selected.type = 'album'; selected.id = item.Id; });
        listEl.appendChild(li);
      });
    } catch (e) {
      statusEl.textContent = 'Failed to load audiobooks.';
    }
  }

  // Download
  document.getElementById('bd-download-btn').addEventListener('click', function () {
    var statusEl = document.getElementById('bd-download-status');
    if (!selected.type || !selected.id) { statusEl.textContent = 'Please select an item first.'; return; }

    var base = serverUrl();
    var url;
    if (selected.type === 'playlist')  url = base + '/BulkDownload/playlist/' + selected.id + '/zip';
    else if (selected.type === 'series') url = base + '/BulkDownload/series/'  + selected.id + '/zip';
    else if (selected.type === 'season') url = base + '/BulkDownload/season/'  + selected.id + '/zip';
    else if (selected.type === 'album')  url = base + '/BulkDownload/album/'   + selected.id + '/zip';

    url += '?ApiKey=' + encodeURIComponent(token());

    var a = document.createElement('a');
    a.href = url;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);

    statusEl.textContent = "Download started — check your browser's download bar.";
  });

  // Utility
  function escHtml(str) {
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  // Init
  var _page = document.getElementById('bulkdownload-page');
  if (_page) {
    _page.addEventListener('pageshow', function () {
      loadPlaylists();
      loadTvShows();
      loadAudiobooks();
    });
  } else {
    loadPlaylists();
    loadTvShows();
    loadAudiobooks();
  }
}());
