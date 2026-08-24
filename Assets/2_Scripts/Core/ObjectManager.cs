using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    // 씬 어디서든 ObjectManager.Instance로 접근하기 위한 싱글톤
    public static ObjectManager Instance { get; private set; }

    // 파괴 대상 오브젝트 26종. WeaponManager의 티어 구조(3,3,3,3,3,3,2,2,2,2)와 1:1로 매칭됨
    private static readonly List<ObjectData> objects = new List<ObjectData>
    {
        new ObjectData(1, 1, "Plate"),
        new ObjectData(1, 2, "Glass Cup"),
        new ObjectData(1, 3, "Flower Pot"),
        new ObjectData(2, 1, "Window"),
        new ObjectData(2, 2, "Wooden Chair"),
        new ObjectData(2, 3, "Wooden Desk"),
        new ObjectData(3, 1, "Brick Wall"),
        new ObjectData(3, 2, "Door"),
        new ObjectData(3, 3, "Side Table"),
        new ObjectData(4, 1, "Wardrobe"),
        new ObjectData(4, 2, "Refrigerator"),
        new ObjectData(4, 3, "Sofa"),
        new ObjectData(5, 1, "Bathroom"),
        new ObjectData(5, 2, "Studio Apartment"),
        new ObjectData(5, 3, "Apartment"),
        new ObjectData(6, 1, "Low-rise Building"),
        new ObjectData(6, 2, "High-rise Building"),
        new ObjectData(6, 3, "City Block"),
        new ObjectData(7, 1, "Entire City"),
        new ObjectData(7, 2, "Metropolis"),
        new ObjectData(8, 1, "Mountain Range"),
        new ObjectData(8, 2, "Continent"),
        new ObjectData(9, 1, "Planet (Earth-class)"),
        new ObjectData(9, 2, "Gas Giant"),
        new ObjectData(10, 1, "Star System"),
        new ObjectData(10, 2, "Galaxy"),
    };

    private int currentObjectIndex; // 현재 선택된 오브젝트의 objects 리스트 인덱스 (0부터 시작)

    public ObjectData CurrentObject => objects[currentObjectIndex];

    // 선택된 오브젝트가 바뀔 때마다 새 오브젝트 데이터를 전달 - 오브젝트 UI 등이 구독
    public event Action<ObjectData> OnObjectChanged;

    void Awake()
    {
        // 씬에 ObjectManager가 중복으로 존재하지 않도록 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 지금은 제한 없이 자유롭게 다음/이전 오브젝트로 전환 (WeaponManager와 동일한 정책)
    public void SelectNext()
    {
        if (currentObjectIndex >= objects.Count - 1)
            return;

        currentObjectIndex++;
        OnObjectChanged?.Invoke(CurrentObject);
    }

    public void SelectPrevious()
    {
        if (currentObjectIndex <= 0)
            return;

        currentObjectIndex--;
        OnObjectChanged?.Invoke(CurrentObject);
    }
}
