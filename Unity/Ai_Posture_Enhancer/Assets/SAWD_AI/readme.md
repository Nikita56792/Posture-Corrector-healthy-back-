# Демо для Function Calling в юнити (на основе Сбер Гигачата)

## Установка

установите NuGet для юнити
https://github.com/GlitchEnzo/NuGetForUnity?tab=readme-ov-file#how-do-i-install-nugetforunity

после установки из него ставим вот этот SDK (ищем NuGet в тулбаре юнити) 

NuGet\Install-Package GigaChatSDK -Version 1.0.5

## Использование

Добавьте AIJSONBuilder и AISender на любой объект на сцене (один раз)

Добавьте на методы, которые хотите сделать доступными для AI тег [AI("сюда пишите описание что делает метод, или не пишите вовсе, не обязательно")]
тег можно добавить на методы, поля и аргументы для того что бы описать каждый из них

Пример из семплов:

    [AI("Двигает куб на точку")]
    public void MoveToPoint([AI("Номер точки")] int pointNumber)
    {
        this.transform.position = points[pointNumber].transform.position;
    }

    [AI("Меняет размер")]
    public void Resize([AI("от 0.001 до 2")] int size)
    {
        this.transform.localScale = new Vector3(size, size, size);
    }

    [AI("Меняет цвет")]
    public void Colorize(int r, int g, int b)
    {
        GetComponent<Renderer>().material.color = new Color(r, g, b);
    }

Просто делайте аналогично на любом методе, который может вызвать ИИ