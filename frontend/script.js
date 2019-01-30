document.getElementById('btnSend').onclick = function () {

var formData = new FormData();
var fileInputElement = document.getElementById('fileInput');
formData.append('file', fileInputElement.files[0]);

fetch('https://localhost:44327/api/images/UploadImage', {
		method: 'post',
		mode: "cors", // no-cors, cors, *same-origin
		body: formData
	}).then(function(res) {
		return res.text();
	}).then(function(result) {
		console.log(result);
		document.getElementById("testImg").src = 'https://localhost:44327/images/' + result;
		document.getElementById("testImg").style.display= "block";
	}).catch(function(err) {
		console.error(err);
	});
}