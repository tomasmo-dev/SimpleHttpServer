
let homePageHeader = document.getElementsByClassName('HeadPage');
homePageHeader[0].addEventListener("click", HeadRotator);

let deg = 360;

function HeadRotator() {
    var myDiv = document.getElementById('HeadPG')

    if (deg >= 360){
        myDiv.style = "transform: translate(50%, 20px) rotate("+ deg +"deg)";
        deg -= 360;
    }else{
        myDiv.style = "transform: translate(50%, 20px) rotate("+ deg +"deg)";
        deg += 360;
    }
    
}

for (let index = 0; index < document.getElementsByClassName('circle').length; index++) {
    const element = document.getElementsByClassName('circle')[index];
    element.style = `border-width: 2px;
                     border-radius: 100%;
                     border-style: solid;

                     background-color: #FFFFFF;
                     opacity: 0.7;

                     width: 100px;
                     height: 100px;

                     position: absolute;
                     transform: translate(${Math.floor(Math.random() * 1200)}px, ${Math.random() * (0 - (-120)) + (-120)}px);

                     z-index: -1;`;
}
